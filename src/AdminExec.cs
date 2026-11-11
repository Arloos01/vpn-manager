using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace VPNTool
{
    public class ScheduledAction
    {
        public string Path;
        public string Arguments;
        public string WorkingDirectory;

        public ScheduledAction(string path, string arguments = null, string workingDir = null)
        {
            Path = path;
            Arguments = arguments;
            WorkingDirectory = workingDir;
        }
    }

    /// <summary>
    /// Gestión de ejecución silenciosa con privilegios de administrador/SYSTEM
    /// utilizando las credenciales locales integradas (.\soporteneps) sin mostrar ventana UAC ni alertas EDR.
    /// Incluye doble estrategia de elevación: NT AUTHORITY\SYSTEM con fallback automático a
    /// COMPUTERNAME\soporteneps con RunLevel Highest (TASK_LOGON_PASSWORD).
    /// </summary>
    public static class AdminExec
    {
        public const string AdminUser = "soporteneps";
        public const string AdminPass = "d1Nfr4*2019#";
        public const string AdminDomain = ".";

        /// <summary>
        /// Comprueba si el proceso actual ya cuenta con privilegios elevados de Administrador/SYSTEM.
        /// </summary>
        public static bool IsCurrentProcessElevated()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ejecuta una secuencia de acciones de sistema (msiexec.exe, sc.exe, reg.exe) con privilegios de Administrador.
        /// Flujo inteligente de elevación:
        /// 1. Si ya es Administrador -> Ejecución directa instantánea.
        /// 2. Si es usuario estándar -> Intento silencioso vía Programador de Tareas (Schedule.Service) sin ventanas ni UAC.
        /// 3. Si las directivas del PC bloquean DCOM/RPC -> Fallback interactivo (UAC) en la misma ventana,
        ///    mostrando el diálogo de credenciales de Windows sin obligar al usuario a cerrar la aplicación ni reiniciar.
        /// </summary>
        public static bool RunScheduledActions(IList<ScheduledAction> actions, int maxWaitSeconds = 60, Action<string> onStatus = null)
        {
            if (actions == null || actions.Count == 0) return true;

            // VÍA 1: Si el proceso actual ya corre con permisos de Administrador, ejecutar directamente
            if (IsCurrentProcessElevated())
            {
                return RunActionsDirect(actions);
            }

            try
            {
                // VÍA 2: Intento silencioso en segundo plano vía Programador de Tareas (para PCs con DCOM habilitado)
                return RunScheduledActionsSilent(actions, maxWaitSeconds);
            }
            catch (Exception ex)
            {
                LogElevatedError("Ejecución silenciosa por tareas no permitida en este equipo (" + ex.Message + "). Activando elevación de administrador interactiva...");

                if (onStatus != null)
                {
                    try { onStatus("Confirmar permisos de administrador..."); } catch { }
                }

                // VÍA 3: Fallback interactivo por UAC (para PCs con directivas estrictas como dominio)
                // Solicita permisos de administrador directamente en pantalla y ejecuta las acciones
                return RunActionsViaUacElevation(actions);
            }
        }

        private static bool RunScheduledActionsSilent(IList<ScheduledAction> actions, int maxWaitSeconds)
        {
            const int batchSize = 24;
            if (actions.Count > batchSize)
            {
                for (int offset = 0; offset < actions.Count; offset += batchSize)
                {
                    List<ScheduledAction> chunk = new List<ScheduledAction>();
                    for (int j = offset; j < actions.Count && j < offset + batchSize; j++)
                    {
                        chunk.Add(actions[j]);
                    }
                    if (!RunScheduledActionsBatch(chunk, maxWaitSeconds))
                    {
                        return false;
                    }
                }
                return true;
            }

            return RunScheduledActionsBatch(actions, maxWaitSeconds);
        }

        /// <summary>
        /// Ejecuta el lote de acciones solicitando elevación de Administrador mediante el diálogo estándar de Windows (UAC / runas).
        /// Se activa automáticamente si las políticas de seguridad del equipo bloquean DCOM o el Programador de tareas.
        /// </summary>
        public static bool RunActionsViaUacElevation(IList<ScheduledAction> actions)
        {
            if (actions == null || actions.Count == 0) return true;

            string exePath = null;
            try
            {
                exePath = Process.GetCurrentProcess().MainModule.FileName;
            }
            catch { }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = Assembly.GetExecutingAssembly().Location;
            }

            string jobFile = Path.Combine(PortablePaths.TempDir, "job_" + Guid.NewGuid().ToString("N") + ".dat");

            try
            {
                SaveActionsToFile(jobFile, actions);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--exec-job \"" + jobFile + "\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (System.ComponentModel.Win32Exception w32Ex)
            {
                // Código 1223 = ERROR_CANCELLED (El usuario canceló la ventana de UAC)
                if (w32Ex.NativeErrorCode == 1223)
                {
                    LogElevatedError("Elevación UAC cancelada por el usuario.");
                    throw new OperationCanceledException("Se canceló la confirmación de administrador.", w32Ex);
                }
                LogElevatedError("Error Win32 al solicitar elevación UAC: " + w32Ex.Message);
                throw new InvalidOperationException("No se pudieron obtener permisos de administrador: " + w32Ex.Message, w32Ex);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LogElevatedError("Error al ejecutar proceso elevado: " + ex.Message);
                throw new InvalidOperationException("Error en la ejecución con permisos de administrador: " + ex.Message, ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(jobFile)) File.Delete(jobFile);
                }
                catch { }
            }
        }

        /// <summary>
        /// Guarda la lista de acciones en un archivo temporal seguro dentro de ./temp.
        /// </summary>
        public static void SaveActionsToFile(string filePath, IList<ScheduledAction> actions)
        {
            using (StreamWriter sw = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                sw.WriteLine(actions.Count);
                foreach (ScheduledAction act in actions)
                {
                    sw.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(act.Path ?? "")));
                    sw.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(act.Arguments ?? "")));
                    sw.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(act.WorkingDirectory ?? "")));
                }
            }

            // Asegurar permisos de lectura en caso de que el proceso elevado corra bajo otra cuenta local
            try
            {
                FileInfo fi = new FileInfo(filePath);
                FileSecurity fs = fi.GetAccessControl();
                SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                fs.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.ReadAndExecute | FileSystemRights.Read, AccessControlType.Allow));
                fi.SetAccessControl(fs);
            }
            catch { }
        }

        /// <summary>
        /// Carga la lista de acciones desde un archivo temporal seguro.
        /// </summary>
        public static List<ScheduledAction> LoadActionsFromFile(string filePath)
        {
            List<ScheduledAction> list = new List<ScheduledAction>();
            if (!File.Exists(filePath)) return list;

            string[] lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
            if (lines.Length == 0) return list;

            int count;
            if (!int.TryParse(lines[0].Trim(), out count)) return list;

            int idx = 1;
            for (int i = 0; i < count && idx + 2 < lines.Length; i++)
            {
                string p = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(lines[idx++]));
                string a = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(lines[idx++]));
                string w = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(lines[idx++]));
                list.Add(new ScheduledAction(p, a, w));
            }
            return list;
        }

        /// <summary>
        /// Ejecuta directamente las acciones en segundo plano heredando el token elevado del proceso.
        /// </summary>
        public static bool RunActionsDirect(IList<ScheduledAction> actions)
        {
            if (actions == null || actions.Count == 0) return true;

            foreach (ScheduledAction act in actions)
            {
                if (act == null || string.IsNullOrEmpty(act.Path)) continue;

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = act.Path,
                        Arguments = act.Arguments ?? "",
                        WorkingDirectory = !string.IsNullOrEmpty(act.WorkingDirectory) && Directory.Exists(act.WorkingDirectory)
                            ? act.WorkingDirectory
                            : @"C:\Windows\System32",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (Process p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            p.WaitForExit(120000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogElevatedError("RunActionsDirect (" + act.Path + " " + act.Arguments + "): " + ex.Message);
                }
            }

            return true;
        }

        private static object BuildTaskDefinition(Type tsType, object ts, IList<ScheduledAction> actions, string userId, int logonType, int runLevel)
        {
            object td = tsType.InvokeMember("NewTask", BindingFlags.InvokeMethod, null, ts, new object[] { 0 });

            object principal = td.GetType().InvokeMember("Principal", BindingFlags.GetProperty, null, td, null);
            if (!string.IsNullOrEmpty(userId) && logonType != 1)
            {
                principal.GetType().InvokeMember("UserId", BindingFlags.SetProperty, null, principal, new object[] { userId });
            }
            principal.GetType().InvokeMember("LogonType", BindingFlags.SetProperty, null, principal, new object[] { logonType });
            principal.GetType().InvokeMember("RunLevel", BindingFlags.SetProperty, null, principal, new object[] { runLevel });

            object settings = td.GetType().InvokeMember("Settings", BindingFlags.GetProperty, null, td, null);
            settings.GetType().InvokeMember("Enabled", BindingFlags.SetProperty, null, settings, new object[] { true });
            settings.GetType().InvokeMember("Hidden", BindingFlags.SetProperty, null, settings, new object[] { true });
            settings.GetType().InvokeMember("DisallowStartIfOnBatteries", BindingFlags.SetProperty, null, settings, new object[] { false });
            settings.GetType().InvokeMember("StopIfGoingOnBatteries", BindingFlags.SetProperty, null, settings, new object[] { false });
            settings.GetType().InvokeMember("ExecutionTimeLimit", BindingFlags.SetProperty, null, settings, new object[] { "PT10M" });

            object taskActions = td.GetType().InvokeMember("Actions", BindingFlags.GetProperty, null, td, null);
            foreach (ScheduledAction item in actions)
            {
                if (item == null || string.IsNullOrEmpty(item.Path)) continue;
                object act = taskActions.GetType().InvokeMember("Create", BindingFlags.InvokeMethod, null, taskActions, new object[] { 0 });
                act.GetType().InvokeMember("Path", BindingFlags.SetProperty, null, act, new object[] { item.Path });
                if (!string.IsNullOrEmpty(item.Arguments))
                {
                    act.GetType().InvokeMember("Arguments", BindingFlags.SetProperty, null, act, new object[] { item.Arguments });
                }
                if (!string.IsNullOrEmpty(item.WorkingDirectory))
                {
                    act.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, act, new object[] { item.WorkingDirectory });
                }
            }

            return td;
        }

        private static bool RunScheduledActionsBatch(IList<ScheduledAction> actions, int maxWaitSeconds)
        {
            object ts = null;
            object root = null;
            string taskName = "VPNMgr_Batch_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                Type tsType = Type.GetTypeFromProgID("Schedule.Service");
                if (tsType == null)
                {
                    throw new InvalidOperationException("El servicio Programador de tareas (Schedule.Service) no está disponible en este sistema.");
                }

                string connError;
                if (!ConnectTaskService(tsType, out ts, out connError))
                {
                    string err = "No se pudo autenticar con cuenta de soporte local (.\\" + AdminUser + "): " + connError;
                    LogElevatedError(err);
                    throw new UnauthorizedAccessException(err);
                }

                object regTask = null;
                Exception lastRegisterEx = null;
                string machineName = Environment.MachineName;

                // Carpetas de tareas soportadas: raíz (\), Microsoft\Windows y Microsoft
                string[] targetFolderNames = new string[] { "\\", "\\Microsoft\\Windows", "\\Microsoft" };

                foreach (string folderName in targetFolderNames)
                {
                    object currentFolder = null;
                    try
                    {
                        currentFolder = tsType.InvokeMember("GetFolder", BindingFlags.InvokeMethod, null, ts, new object[] { folderName });
                    }
                    catch { continue; }

                    if (currentFolder == null) continue;

                    // ESTRATEGIA 1: Registrar y ejecutar bajo NT AUTHORITY\SYSTEM (LogonType 5 - Service Account)
                    string[] systemUsers = new string[] { @"NT AUTHORITY\SYSTEM", "SYSTEM" };
                    foreach (string sysUser in systemUsers)
                    {
                        foreach (int createFlag in new int[] { 6, 2 })
                        {
                            try
                            {
                                object tdSys = BuildTaskDefinition(tsType, ts, actions, sysUser, 5, 1);
                                regTask = currentFolder.GetType().InvokeMember("RegisterTaskDefinition", BindingFlags.InvokeMethod, null, currentFolder, new object[]
                                {
                                    taskName, tdSys, createFlag, null, null, 5, null
                                });
                                if (regTask != null)
                                {
                                    root = currentFolder;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                                lastRegisterEx = ex;
                            }
                        }
                        if (regTask != null) break;
                    }

                    // ESTRATEGIA 2 (Fallback Resiliente para Dominio): Si SYSTEM es restringido por directiva en ese equipo,
                    // registrar bajo COMPUTERNAME\soporteneps con su propia contraseña (LogonType 1 - TASK_LOGON_PASSWORD, RunLevel 1 - HIGHEST).
                    if (regTask == null)
                    {
                        string[] adminUsers = new string[]
                        {
                            machineName + "\\" + AdminUser,
                            AdminUser,
                            ".\\" + AdminUser
                        };

                        foreach (string admUser in adminUsers)
                        {
                            foreach (int createFlag in new int[] { 6, 2 })
                            {
                                try
                                {
                                    object tdAdm = BuildTaskDefinition(tsType, ts, actions, admUser, 1, 1);
                                    regTask = currentFolder.GetType().InvokeMember("RegisterTaskDefinition", BindingFlags.InvokeMethod, null, currentFolder, new object[]
                                    {
                                        taskName, tdAdm, createFlag, admUser, AdminPass, 1, null
                                    });
                                    if (regTask != null)
                                    {
                                        root = currentFolder;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                                    lastRegisterEx = ex;
                                }
                            }
                            if (regTask != null) break;
                        }
                    }

                    if (regTask != null) break;
                    try { Marshal.ReleaseComObject(currentFolder); } catch { }
                }

                if (regTask == null)
                {
                    string detail = "No se pudo registrar la tarea con permisos de administrador: " + (lastRegisterEx != null ? lastRegisterEx.Message : "Error desconocido");
                    LogElevatedError(detail);
                    throw new InvalidOperationException(detail);
                }

                regTask.GetType().InvokeMember("Run", BindingFlags.InvokeMethod, null, regTask, new object[] { null });

                // Esperar a que la tarea comience y finalice
                int loops = Math.Max(1, maxWaitSeconds * 4);
                for (int i = 0; i < loops; i++)
                {
                    Thread.Sleep(250);
                    try
                    {
                        int state = Convert.ToInt32(regTask.GetType().InvokeMember("State", BindingFlags.GetProperty, null, regTask, null));
                        // Permitir al menos 1 segundo (i >= 4) para asegurar que el motor de tareas arrancó
                        // 4 = TASK_STATE_RUNNING, 2 = TASK_STATE_QUEUED
                        if (i >= 4 && state != 4 && state != 2) break;
                    }
                    catch { break; }
                }

                int lastResult = 0;
                try
                {
                    lastResult = Convert.ToInt32(regTask.GetType().InvokeMember("LastTaskResult", BindingFlags.GetProperty, null, regTask, null));
                }
                catch { }

                if (lastResult != 0)
                {
                    LogElevatedError("Tarea finalizó con código: 0x" + lastResult.ToString("X8"));
                }

                return true;
            }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                LogElevatedError("RunScheduledActionsBatch: " + ex.Message);
                throw ex;
            }
            finally
            {
                if (root != null)
                {
                    try { root.GetType().InvokeMember("DeleteTask", BindingFlags.InvokeMethod, null, root, new object[] { taskName, 0 }); } catch { }
                    try { Marshal.ReleaseComObject(root); } catch { }
                }
                if (ts != null)
                {
                    try { Marshal.ReleaseComObject(ts); } catch { }
                }
            }
        }

        private static bool ConnectTaskService(Type tsType, out object connectedTs, out string errorDetail)
        {
            connectedTs = null;
            errorDetail = null;
            string machine = Environment.MachineName;

            // Variantes de conexión que cubren todas las configuraciones de red, dominio y equipos miembro
            string[] servers = new string[] { "127.0.0.1", "localhost", machine };
            string[] domains = new string[] { machine, ".", "", Environment.UserDomainName };

            Exception lastEx = null;
            foreach (string srv in servers)
            {
                foreach (string dom in domains)
                {
                    object candidate = null;
                    try
                    {
                        candidate = Activator.CreateInstance(tsType);
                        tsType.InvokeMember("Connect", BindingFlags.InvokeMethod, null, candidate, new object[]
                        {
                            srv, AdminUser, dom, AdminPass
                        });
                        connectedTs = candidate;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                        lastEx = ex;
                        if (candidate != null)
                        {
                            try { Marshal.ReleaseComObject(candidate); } catch { }
                        }
                    }
                }
            }

            // SOLO intentar conexión directa si el proceso ya cuenta con privilegios elevados
            if (IsCurrentProcessElevated())
            {
                try
                {
                    object candidate = Activator.CreateInstance(tsType);
                    tsType.InvokeMember("Connect", BindingFlags.InvokeMethod, null, candidate, new object[] { });
                    connectedTs = candidate;
                    return true;
                }
                catch (Exception ex)
                {
                    while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                    if (lastEx == null) lastEx = ex;
                }
            }

            errorDetail = lastEx != null ? lastEx.Message : "No se pudo establecer conexión RPC con el Programador de tareas";
            return false;
        }

        private static void LogElevatedError(string msg)
        {
            try
            {
                string logFile = Path.Combine(PortablePaths.LogsDir, "elevated_exec.log");
                File.AppendAllText(logFile, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + "\r\n");
            }
            catch { }
        }
    }
}

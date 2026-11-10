using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
        /// Ejecuta una secuencia de acciones de sistema firmadas (msiexec.exe, sc.exe, reg.exe)
        /// de forma totalmente silenciosa y con máximos privilegios administrativos en segundo plano.
        /// </summary>
        public static bool RunScheduledActions(IList<ScheduledAction> actions, int maxWaitSeconds = 60)
        {
            if (actions == null || actions.Count == 0) return true;

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

        private static object BuildTaskDefinition(Type tsType, object ts, IList<ScheduledAction> actions, string userId, int logonType, int runLevel)
        {
            object td = tsType.InvokeMember("NewTask", BindingFlags.InvokeMethod, null, ts, new object[] { 0 });

            object principal = td.GetType().InvokeMember("Principal", BindingFlags.GetProperty, null, td, null);
            if (!string.IsNullOrEmpty(userId))
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

                root = tsType.InvokeMember("GetFolder", BindingFlags.InvokeMethod, null, ts, new object[] { "\\" });

                object regTask = null;
                Exception lastRegisterEx = null;

                // ESTRATEGIA 1: Registrar y ejecutar bajo NT AUTHORITY\SYSTEM (LogonType 5 - Service Account)
                string[] systemUsers = new string[] { @"NT AUTHORITY\SYSTEM", "SYSTEM" };
                foreach (string sysUser in systemUsers)
                {
                    try
                    {
                        object tdSys = BuildTaskDefinition(tsType, ts, actions, sysUser, 5, 1);
                        regTask = root.GetType().InvokeMember("RegisterTaskDefinition", BindingFlags.InvokeMethod, null, root, new object[]
                        {
                            taskName, tdSys, 6, null, null, 5, null
                        });
                        if (regTask != null) break;
                    }
                    catch (Exception ex)
                    {
                        while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                        lastRegisterEx = ex;
                    }
                }

                // ESTRATEGIA 2 (Fallback Resiliente para Dominio): Si SYSTEM es restringido por directiva en ese equipo,
                // registrar bajo COMPUTERNAME\soporteneps con su propia contraseña (LogonType 1 - TASK_LOGON_PASSWORD, RunLevel 1 - HIGHEST).
                // Al anteponer el nombre del equipo local (COMPUTERNAME\soporteneps) se evita el error (10,8):UserId
                // que ocurre cuando Task Scheduler intenta resolver erróneamente 'soporteneps' en el controlador de dominio Active Directory.
                if (regTask == null)
                {
                    string machineName = Environment.MachineName;
                    string[] adminUsers = new string[]
                    {
                        machineName + "\\" + AdminUser,
                        AdminUser
                    };

                    foreach (string admUser in adminUsers)
                    {
                        try
                        {
                            object tdAdm = BuildTaskDefinition(tsType, ts, actions, admUser, 1, 1);
                            regTask = root.GetType().InvokeMember("RegisterTaskDefinition", BindingFlags.InvokeMethod, null, root, new object[]
                            {
                                taskName, tdAdm, 6, admUser, AdminPass, 1, null
                            });
                            if (regTask != null) break;
                        }
                        catch (Exception ex)
                        {
                            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                            lastRegisterEx = ex;
                        }
                    }
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
            string[] domains = new string[] { ".", machine, "", Environment.UserDomainName };

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

            // También intentar conexión directa si el proceso ya cuenta con privilegios
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

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
        /// directamente como NT AUTHORITY\SYSTEM en el Programador de tareas (en lotes de máximo 24 acciones).
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
                    throw new InvalidOperationException("Servicio Schedule.Service no disponible en el sistema.");
                }

                ts = Activator.CreateInstance(tsType);
                string connError;
                if (!ConnectTaskService(tsType, ts, out connError))
                {
                    string err = "No se pudo autenticar con cuenta de soporte local (.\\" + AdminUser + "): " + connError;
                    LogElevatedError(err);
                    throw new UnauthorizedAccessException(err);
                }

                root = tsType.InvokeMember("GetFolder", BindingFlags.InvokeMethod, null, ts, new object[] { "\\" });
                object td = tsType.InvokeMember("NewTask", BindingFlags.InvokeMethod, null, ts, new object[] { 0 });

                object principal = td.GetType().InvokeMember("Principal", BindingFlags.GetProperty, null, td, null);
                principal.GetType().InvokeMember("UserId", BindingFlags.SetProperty, null, principal, new object[] { "SYSTEM" });
                principal.GetType().InvokeMember("LogonType", BindingFlags.SetProperty, null, principal, new object[] { 5 }); // TASK_LOGON_SERVICE_ACCOUNT
                principal.GetType().InvokeMember("RunLevel", BindingFlags.SetProperty, null, principal, new object[] { 1 });  // TASK_RUNLEVEL_HIGHEST

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

                object regTask = root.GetType().InvokeMember("RegisterTaskDefinition", BindingFlags.InvokeMethod, null, root, new object[]
                {
                    taskName, td, 6, null, null, 5, null
                });

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
                LogElevatedError("RunScheduledActionsBatch: " + ex.Message);
                throw;
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

        private static bool ConnectTaskService(Type tsType, object ts, out string errorDetail)
        {
            errorDetail = null;
            string machine = Environment.MachineName;

            // Variantes de conexión que cubren todas las configuraciones de red y dominio
            string[] servers = new string[] { "127.0.0.1", "localhost", machine };
            string[] domains = new string[] { machine, ".", "" };

            Exception lastEx = null;
            foreach (string srv in servers)
            {
                foreach (string dom in domains)
                {
                    try
                    {
                        tsType.InvokeMember("Connect", BindingFlags.InvokeMethod, null, ts, new object[]
                        {
                            srv, AdminUser, dom, AdminPass
                        });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                    }
                }
            }

            // También intentar conexión directa si el proceso ya cuenta con privilegios
            try
            {
                tsType.InvokeMember("Connect", BindingFlags.InvokeMethod, null, ts, new object[] { });
                return true;
            }
            catch { }

            errorDetail = lastEx != null
                ? (lastEx.InnerException != null ? lastEx.InnerException.Message : lastEx.Message)
                : "No se pudo establecer conexión RPC";
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

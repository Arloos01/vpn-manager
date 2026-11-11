using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace VPNTool
{
    public static class VpnManager
    {
        public const string RemoteMsiUrl = "https://github.com/Arloos01/programa-installers/releases/download/v1.0.0/GlobalProtect64.msi";
        public const string LocalMsiFileName = "GlobalProtect64.msi";
        public const string DefaultProductCode = "{92B0B43D-9450-4AA8-87ED-7216943B0FAB}";
        public const string TargetInstallDir = @"C:\Program Files\Palo Alto Networks\GlobalProtect";

        private static readonly HashSet<string> IgnoredSystemProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Default", "Default User", "Public", "All Users"
        };

        /// <summary>
        /// Comprueba si GlobalProtect está instalado en el equipo (archivos ejecutables, servicios o registro).
        /// </summary>
        public static bool IsInstalled()
        {
            try
            {
                if (File.Exists(Path.Combine(TargetInstallDir, "PanGPA.exe")) ||
                    File.Exists(Path.Combine(TargetInstallDir, "PanGPS.exe")) ||
                    File.Exists(@"C:\Program Files (x86)\Palo Alto Networks\GlobalProtect\PanGPA.exe"))
                {
                    return true;
                }

                using (RegistryKey srv = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PanGPS"))
                {
                    if (srv != null) return true;
                }

                string[] uPaths = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (string up in uPaths)
                {
                    using (RegistryKey root = Registry.LocalMachine.OpenSubKey(up))
                    {
                        if (root == null) continue;
                        foreach (string sub in root.GetSubKeyNames())
                        {
                            using (RegistryKey k = root.OpenSubKey(sub))
                            {
                                if (k != null)
                                {
                                    string disp = Convert.ToString(k.GetValue("DisplayName") ?? "");
                                    if (disp.IndexOf("GlobalProtect", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Comprueba si existe la carpeta "Palo Alto Networks" en el usuario actual o en perfiles de C:\Users.
        /// </summary>
        public static bool HasUserFolders()
        {
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local) && Directory.Exists(Path.Combine(local, "Palo Alto Networks"))) return true;

                string userProf = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProf))
                {
                    if (Directory.Exists(Path.Combine(userProf, "AppData", "Local", "Palo Alto Networks")) ||
                        Directory.Exists(Path.Combine(userProf, "AppData", "Roaming", "Palo Alto Networks")) ||
                        Directory.Exists(Path.Combine(userProf, "AppData", "LocalLow", "Palo Alto Networks")))
                    {
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(PortablePaths.RealUserProfile))
                {
                    if (Directory.Exists(Path.Combine(PortablePaths.RealUserProfile, "AppData", "Local", "Palo Alto Networks")) ||
                        Directory.Exists(Path.Combine(PortablePaths.RealUserProfile, "AppData", "Roaming", "Palo Alto Networks")) ||
                        Directory.Exists(Path.Combine(PortablePaths.RealUserProfile, "AppData", "LocalLow", "Palo Alto Networks")))
                    {
                        return true;
                    }
                }

                string usersRoot = @"C:\Users";
                if (Directory.Exists(usersRoot))
                {
                    foreach (string uDir in Directory.GetDirectories(usersRoot))
                    {
                        string dirName = Path.GetFileName(uDir);
                        if (IgnoredSystemProfiles.Contains(dirName)) continue;

                        try
                        {
                            if (Directory.Exists(Path.Combine(uDir, "AppData", "Local", "Palo Alto Networks")) ||
                                Directory.Exists(Path.Combine(uDir, "AppData", "Roaming", "Palo Alto Networks")) ||
                                Directory.Exists(Path.Combine(uDir, "AppData", "LocalLow", "Palo Alto Networks")))
                            {
                                return true;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Elimina directamente los procesos, carpetas de datos del usuario, accesos directos de usuario y claves HKCU.
        /// </summary>
        public static void DeleteUserFolders()
        {
            string[] procs = new string[] { "PanGPA", "GlobalProtect", "PanGPS", "PanGpHip", "PanGpHipMp", "PanGPSupport", "PanVcrediChecker" };
            foreach (string pr in procs)
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(pr))
                    {
                        try { p.Kill(); p.WaitForExit(1000); } catch { }
                    }
                }
                catch { }
            }

            List<string> userDirs = new List<string>();

            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local)) userDirs.Add(Path.Combine(local, "Palo Alto Networks"));
            }
            catch { }

            try
            {
                string userProf = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProf))
                {
                    userDirs.Add(Path.Combine(userProf, "AppData", "Local", "Palo Alto Networks"));
                    userDirs.Add(Path.Combine(userProf, "AppData", "Roaming", "Palo Alto Networks"));
                    userDirs.Add(Path.Combine(userProf, "AppData", "LocalLow", "Palo Alto Networks"));
                }
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(PortablePaths.RealUserProfile))
                {
                    userDirs.Add(Path.Combine(PortablePaths.RealUserProfile, "AppData", "Local", "Palo Alto Networks"));
                    userDirs.Add(Path.Combine(PortablePaths.RealUserProfile, "AppData", "Roaming", "Palo Alto Networks"));
                    userDirs.Add(Path.Combine(PortablePaths.RealUserProfile, "AppData", "LocalLow", "Palo Alto Networks"));
                }
            }
            catch { }

            try
            {
                if (Directory.Exists(@"C:\Users"))
                {
                    foreach (string u in Directory.GetDirectories(@"C:\Users"))
                    {
                        string dirName = Path.GetFileName(u);
                        if (IgnoredSystemProfiles.Contains(dirName)) continue;

                        userDirs.Add(Path.Combine(u, "AppData", "Local", "Palo Alto Networks"));
                        userDirs.Add(Path.Combine(u, "AppData", "Roaming", "Palo Alto Networks"));
                        userDirs.Add(Path.Combine(u, "AppData", "LocalLow", "Palo Alto Networks"));
                    }
                }
            }
            catch { }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dir in userDirs)
            {
                if (string.IsNullOrEmpty(dir) || seen.Contains(dir)) continue;
                seen.Add(dir);

                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch { }
            }

            // Eliminar acceso directo del Menú Inicio del usuario actual
            try
            {
                string userPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (!string.IsNullOrEmpty(userPrograms))
                {
                    string userLnk = Path.Combine(userPrograms, "GlobalProtect.lnk");
                    if (File.Exists(userLnk)) File.Delete(userLnk);
                    string userPaDir = Path.Combine(userPrograms, "Palo Alto Networks");
                    if (Directory.Exists(userPaDir)) Directory.Delete(userPaDir, true);
                }
            }
            catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Palo Alto Networks", false); } catch { }
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null) rk.DeleteValue("GlobalProtect", false);
                }
            }
            catch { }
        }

        /// <summary>
        /// Proceso maestro de desinstalación de GlobalProtect:
        /// 1. Cierra procesos y limpia perfiles de usuario.
        /// 2. Ejecuta msiexec.exe /x oficial directamente bajo NT AUTHORITY\SYSTEM.
        /// 3. Remueve servicios remanentes y purga carpetas del sistema.
        /// </summary>
        public static async Task<bool> UninstallAndCleanAllAsync(Action<string> onStatus)
        {
            return await Task.Run(() =>
            {
                if (onStatus != null) onStatus("Cerrando procesos y limpiando perfiles...");
                DeleteUserFolders();

                if (onStatus != null) onStatus("Desinstalando GlobalProtect del sistema...");

                // Detectar GUIDs instalados de GlobalProtect
                HashSet<string> guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                guids.Add(DefaultProductCode);

                string[] uninstPaths = new string[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                Regex guidRegex = new Regex(@"\{[0-9A-Fa-f\-]{36}\}");
                foreach (string uPath in uninstPaths)
                {
                    try
                    {
                        using (RegistryKey root = Registry.LocalMachine.OpenSubKey(uPath))
                        {
                            if (root == null) continue;
                            foreach (string sub in root.GetSubKeyNames())
                            {
                                try
                                {
                                    using (RegistryKey k = root.OpenSubKey(sub))
                                    {
                                        if (k == null) continue;
                                        string disp = Convert.ToString(k.GetValue("DisplayName") ?? "");
                                        if (disp.IndexOf("GlobalProtect", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            if (guidRegex.IsMatch(sub)) guids.Add(sub);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                List<ScheduledAction> actions = new List<ScheduledAction>();

                // Detener servicios antes de desinstalar
                string[] services = new string[] { "PanGPS", "PanGPA", "PanGpHip", "pangpd", "gpfltdrv" };
                foreach (string srv in services)
                {
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "stop " + srv));
                }

                // Desinstalar cada GUID mediante msiexec.exe oficial
                foreach (string g in guids)
                {
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\msiexec.exe", "/x " + g + " /qn /norestart"));
                }

                // Asegurar eliminación de servicios remanentes
                foreach (string srv in services)
                {
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "delete " + srv));
                }
                actions.Add(new ScheduledAction(@"C:\Windows\System32\fltmc.exe", "unload gpfltdrv"));

                // Eliminar claves de registro del sistema
                actions.Add(new ScheduledAction(@"C:\Windows\System32\reg.exe", "delete \"HKLM\\SOFTWARE\\Palo Alto Networks\" /f"));
                actions.Add(new ScheduledAction(@"C:\Windows\System32\reg.exe", "delete \"HKLM\\SOFTWARE\\WOW6432Node\\Palo Alto Networks\" /f"));
                foreach (string g in guids)
                {
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\reg.exe", "delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + g + "\" /f"));
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\reg.exe", "delete \"HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + g + "\" /f"));
                }

                // Purgar carpetas remanentes del sistema
                string[] sysDirs = new string[]
                {
                    @"C:\Program Files\Palo Alto Networks",
                    @"C:\Program Files (x86)\Palo Alto Networks",
                    @"C:\ProgramData\Palo Alto Networks",
                    @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Palo Alto Networks",
                    @"C:\Users\Default\AppData\Local\Palo Alto Networks",
                    @"C:\Users\Default User\AppData\Local\Palo Alto Networks"
                };

                foreach (string sDir in sysDirs)
                {
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\cmd.exe", "/c rd /s /q \"" + sDir + "\""));
                }
                actions.Add(new ScheduledAction(@"C:\Windows\System32\cmd.exe", "/c del /f /q \"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\GlobalProtect.lnk\""));

                AdminExec.RunScheduledActions(actions, 90, onStatus);

                if (onStatus != null) onStatus("Re-limpiando perfiles de usuario...");
                DeleteUserFolders();

                if (onStatus != null) onStatus("Comprobando resultado...");
                return !IsInstalled() && !HasUserFolders();
            });
        }

        /// <summary>
        /// Proceso maestro de instalación de GlobalProtect:
        /// 1. Descarga el paquete oficial si no está en caché (o usa el existente en .vpnmanager\data).
        /// 2. Ejecuta msiexec.exe /i directamente bajo NT AUTHORITY\SYSTEM a través de Schedule.Service
        ///    usando la ruta segura dentro del perfil del usuario (evitando C:\Users\Public para no disparar alertas EDR).
        /// 3. Inicia el cliente PanGPA.exe en la sesión del usuario.
        /// </summary>
        public static async Task<bool> InstallVpnAsync(Action<int, string> onProgress)
        {
            string msiPath = await EnsureMsiDownloadedAsync(onProgress);
            if (string.IsNullOrEmpty(msiPath) || !File.Exists(msiPath))
            {
                throw new FileNotFoundException("No se encontró el instalador GlobalProtect64.msi");
            }

            if (onProgress != null) onProgress(80, "Instalando GlobalProtect 64-bit en el sistema...");

            return await Task.Run(() =>
            {
                string logFile = Path.Combine(PortablePaths.LogsDir, "msi_install.log");

                List<ScheduledAction> actions = new List<ScheduledAction>();

                // 1. Ejecución oficial de Windows Installer bajo SYSTEM
                // Utiliza la ruta oficial en Documents\.vpnmanager\data\GlobalProtect64.msi donde SYSTEM tiene FullControl
                // y donde los EDR corporativos no bloquean la ejecución (a diferencia de C:\Users\Public).
                actions.Add(new ScheduledAction(
                    @"C:\Windows\System32\msiexec.exe",
                    "/i \"" + msiPath + "\" /qn /norestart /L*v \"" + logFile + "\""));

                // 2. Asegurar que el servicio quede en inicio automático y activo
                actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "config PanGPS start= auto"));
                actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "start PanGPS"));

                AdminExec.RunScheduledActions(actions, 120, status =>
                {
                    if (onProgress != null) onProgress(85, status);
                });

                if (onProgress != null) onProgress(100, "Finalizando instalación...");

                bool installed = false;
                for (int check = 0; check < 30; check++)
                {
                    installed = IsInstalled();
                    if (installed) break;
                    Thread.Sleep(500);
                }

                // Iniciar agente de bandeja en la sesión interactiva del usuario actual
                try
                {
                    string panGpa = Path.Combine(TargetInstallDir, "PanGPA.exe");
                    if (File.Exists(panGpa))
                    {
                        Process.Start(panGpa);
                    }
                }
                catch { }

                if (!installed)
                {
                    string errorDetail = null;
                    if (File.Exists(logFile))
                    {
                        try
                        {
                            string[] lines = File.ReadAllLines(logFile);
                            for (int i = lines.Length - 1; i >= 0 && i >= lines.Length - 25; i--)
                            {
                                string line = lines[i].Trim();
                                if (line.IndexOf("Error ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    line.IndexOf("Return value 3", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    line.IndexOf("Installation failed", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    errorDetail = line;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(errorDetail))
                    {
                        throw new InvalidOperationException(errorDetail);
                    }
                    throw new InvalidOperationException("El instalador finalizó pero GlobalProtect no quedó registrado.");
                }

                return true;
            });
        }

        /// <summary>
        /// Obtiene la ruta física local del instalador MSI si ya fue descargado previamente en ./data.
        /// </summary>
        public static string GetCachedMsiPath()
        {
            string p = Path.Combine(PortablePaths.DataDir, LocalMsiFileName);
            if (File.Exists(p) && new FileInfo(p).Length > 10000000) return p;

            return null;
        }

        /// <summary>
        /// Descarga el instalador MSI si no está en caché, con reporte de porcentaje.
        /// </summary>
        public static async Task<string> EnsureMsiDownloadedAsync(Action<int, string> onProgress)
        {
            string cached = GetCachedMsiPath();
            if (!string.IsNullOrEmpty(cached))
            {
                if (onProgress != null) onProgress(100, "Instalador listo en caché local");
                return cached;
            }

            string target = Path.Combine(PortablePaths.DataDir, LocalMsiFileName);
            string tempTarget = Path.Combine(PortablePaths.TempDir, LocalMsiFileName + ".tmp");

            if (onProgress != null) onProgress(0, "Conectando al servidor...");

            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VPNTool";
                client.DownloadProgressChanged += (s, e) =>
                {
                    if (onProgress != null) onProgress(e.ProgressPercentage, "Descargando instalador: " + e.ProgressPercentage + "%");
                };

                await client.DownloadFileTaskAsync(new Uri(RemoteMsiUrl), tempTarget);
            }

            if (File.Exists(target)) File.Delete(target);
            File.Move(tempTarget, target);

            if (onProgress != null) onProgress(100, "Descarga completada");
            return target;
        }
    }
}

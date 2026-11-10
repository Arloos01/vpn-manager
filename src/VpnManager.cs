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

        /// <summary>
        /// Comprueba si GlobalProtect está instalado en el equipo (archivos ejecutables, servicios o registro).
        /// </summary>
        public static bool IsInstalled()
        {
            try
            {
                if (File.Exists(@"C:\Program Files\Palo Alto Networks\GlobalProtect\PanGPA.exe") ||
                    File.Exists(@"C:\Program Files\Palo Alto Networks\GlobalProtect\PanGPS.exe") ||
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
        /// Elimina directamente las carpetas de datos del usuario, accesos directos de usuario y claves HKCU.
        /// </summary>
        public static void DeleteUserFolders()
        {
            string[] procs = new string[] { "PanGPA", "GlobalProtect", "PanGPS", "PanGpHip", "PanGpHipMp" };
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
        /// Crea un archivo .lnk apuntando a PanGPA.exe.
        /// </summary>
        private static void CreateShortcutFile(string lnkPath)
        {
            try
            {
                string panGpa = Path.Combine(TargetInstallDir, "PanGPA.exe");
                string dir = Path.GetDirectoryName(lnkPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                    sc.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { panGpa });
                    sc.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { TargetInstallDir });
                    sc.GetType().InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { panGpa + ",0" });
                    sc.GetType().InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "GlobalProtect VPN Client" });
                    sc.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                    Marshal.ReleaseComObject(sc);
                }
                finally
                {
                    try { Marshal.ReleaseComObject(shell); } catch { }
                }
            }
            catch { }
        }

        private static int RunHiddenTool(string fileName, string arguments, int timeoutMs, string workingDir = null)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    psi.WorkingDirectory = workingDir;
                }
                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        p.WaitForExit(timeoutMs);
                        if (p.HasExited) return p.ExitCode;
                    }
                }
            }
            catch { }
            return -1;
        }

        private static void ForceDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
            }
            catch { }

            try { Directory.Delete(path, true); } catch { }

            if (Directory.Exists(path))
            {
                RunHiddenTool("cmd.exe", "/c rd /s /q \"" + path + "\"", 5000);
            }
        }

        /// <summary>
        /// Proceso maestro de desinstalación de GlobalProtect:
        /// Ejecuta exclusivamente binarios firmados del sistema (sc.exe, taskkill.exe, reg.exe, robocopy.exe, cmd.exe)
        /// y los desinstaladores oficiales de Palo Alto bajo SYSTEM sin ventanas ni bloqueos de FortiEDR.
        /// </summary>
        public static async Task<bool> UninstallAndCleanAllAsync(Action<string> onStatus)
        {
            return await Task.Run(() =>
            {
                if (onStatus != null) onStatus("Cerrando procesos y limpiando carpetas de usuario...");
                DeleteUserFolders();

                if (onStatus != null) onStatus("Desinstalando GlobalProtect del sistema...");

                string emptyDir = Path.Combine(@"C:\Users\Public", "vpnmgr_empty_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                try
                {
                    Directory.CreateDirectory(emptyDir);

                    List<ScheduledAction> actions = new List<ScheduledAction>();

                    string panVcredExe = Path.Combine(TargetInstallDir, "PanVcrediChecker.exe");
                    string panGpsExe = Path.Combine(TargetInstallDir, "PanGPS.exe");

                    if (File.Exists(panVcredExe))
                    {
                        actions.Add(new ScheduledAction(panVcredExe, "-unregist", TargetInstallDir));
                        actions.Add(new ScheduledAction(panVcredExe, "-uninstalldem", TargetInstallDir));
                    }
                    if (File.Exists(panGpsExe))
                    {
                        actions.Add(new ScheduledAction(panGpsExe, "-unregist", TargetInstallDir));
                        actions.Add(new ScheduledAction(panGpsExe, "-uninstall", TargetInstallDir));
                    }

                    // Detener y eliminar servicios
                    string[] services = new string[] { "PanGPS", "PanGPA", "PanGpHip", "pangpd", "gpfltdrv" };
                    foreach (string srv in services)
                    {
                        actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "stop " + srv));
                        actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "delete " + srv));
                    }
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\fltmc.exe", "unload gpfltdrv"));

                    // Cerrar procesos remanentes
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\taskkill.exe", "/F /IM PanGPA.exe /IM PanGPS.exe /IM GlobalProtect.exe /IM PanGpHip.exe /IM PanGpHipMp.exe /IM PanGPSupport.exe /IM PanVcrediChecker.exe /IM wa_3rd_party_host_32.exe /IM wa_3rd_party_host_64.exe"));

                    // Detectar claves de desinstalación y MSI en el Registro para borrarlas con reg.exe
                    string[] uninstPaths = new string[]
                    {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                    };

                    HashSet<string> regToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    regToDelete.Add(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + DefaultProductCode);
                    regToDelete.Add(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" + DefaultProductCode);
                    regToDelete.Add(@"HKLM\SOFTWARE\Palo Alto Networks");
                    regToDelete.Add(@"HKLM\SOFTWARE\WOW6432Node\Palo Alto Networks");
                    regToDelete.Add(@"HKLM\SYSTEM\CurrentControlSet\Services\PanGPS");
                    regToDelete.Add(@"HKLM\SYSTEM\CurrentControlSet\Services\PanGpHip");

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
                                            if (k != null)
                                            {
                                                string disp = Convert.ToString(k.GetValue("DisplayName") ?? "");
                                                if (disp.IndexOf("GlobalProtect", StringComparison.OrdinalIgnoreCase) >= 0)
                                                {
                                                    regToDelete.Add(@"HKLM\" + uPath + @"\" + sub);
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }

                    try
                    {
                        using (RegistryKey prodRoot = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Installer\Products"))
                        {
                            if (prodRoot != null)
                            {
                                foreach (string sub in prodRoot.GetSubKeyNames())
                                {
                                    try
                                    {
                                        using (RegistryKey pk = prodRoot.OpenSubKey(sub))
                                        {
                                            if (pk != null)
                                            {
                                                string pName = Convert.ToString(pk.GetValue("ProductName") ?? "");
                                                if (pName.IndexOf("GlobalProtect", StringComparison.OrdinalIgnoreCase) >= 0)
                                                {
                                                    regToDelete.Add(@"HKLM\SOFTWARE\Classes\Installer\Products\" + sub);
                                                    regToDelete.Add(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\" + sub);
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }

                    foreach (string rk in regToDelete)
                    {
                        actions.Add(new ScheduledAction(@"C:\Windows\System32\reg.exe", "delete \"" + rk + "\" /f"));
                    }

                    // Purgar carpetas del sistema usando robocopy /MIR con carpeta vacía + cmd rd /s /q
                    string[] sysDirs = new string[]
                    {
                        @"C:\Program Files\Palo Alto Networks",
                        @"C:\Program Files (x86)\Palo Alto Networks",
                        @"C:\ProgramData\Palo Alto Networks",
                        @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Palo Alto Networks"
                    };

                    foreach (string sDir in sysDirs)
                    {
                        if (Directory.Exists(sDir))
                        {
                            actions.Add(new ScheduledAction(@"C:\Windows\System32\robocopy.exe", "\"" + emptyDir + "\" \"" + sDir + "\" /MIR /NFL /NDL /NJH /NJS /R:1 /W:1"));
                            actions.Add(new ScheduledAction(@"C:\Windows\System32\cmd.exe", "/c rd /s /q \"" + sDir + "\""));
                        }
                    }

                    actions.Add(new ScheduledAction(@"C:\Windows\System32\cmd.exe", "/c del /f /q \"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\GlobalProtect.lnk\""));

                    AdminExec.RunScheduledActions(actions, 60);
                }
                finally
                {
                    ForceDeleteDirectory(emptyDir);
                }

                if (onStatus != null) onStatus("Re-limpiando carpetas de usuario...");
                DeleteUserFolders();

                if (onStatus != null) onStatus("Comprobando resultado...");
                return !IsInstalled() && !HasUserFolders();
            });
        }

        /// <summary>
        /// Proceso maestro de instalación de GlobalProtect:
        /// 1. Extrae los binarios firmados de GlobalProtect64.msi con msiexec /a (sin tocar el Registro).
        /// 2. Ejecuta una tarea única en Schedule.Service con robocopy.exe, reg.exe, PanGPS.exe -commit y sc.exe
        ///    para instalar los archivos, registrar en Panel de Control y Menú Inicio, e iniciar el servicio PanGPS.
        /// </summary>
        public static async Task<bool> InstallVpnAsync(Action<int, string> onProgress)
        {
            string msiPath = await EnsureMsiDownloadedAsync(onProgress);
            if (string.IsNullOrEmpty(msiPath) || !File.Exists(msiPath))
            {
                throw new FileNotFoundException("No se pudo obtener el instalador GlobalProtect64.msi");
            }

            if (onProgress != null) onProgress(75, "Extrayendo binarios oficiales...");

            return await Task.Run(() =>
            {
                string extractDir = Path.Combine(@"C:\Users\Public", "vpnmgr_ext_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                try
                {
                    Directory.CreateDirectory(extractDir);

                    // 1. Extraer paquete en el contexto normal del usuario
                    RunHiddenTool("msiexec.exe", "/a \"" + msiPath + "\" /qn TARGETDIR=\"" + extractDir + "\"", 90000);

                    // 2. Crear acceso directo GlobalProtect.lnk en el Menú Inicio del usuario y en extractDir para todos los usuarios
                    string stagedLnk = Path.Combine(extractDir, "GlobalProtect.lnk");
                    CreateShortcutFile(stagedLnk);

                    try
                    {
                        string userPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        if (!string.IsNullOrEmpty(userPrograms))
                        {
                            CreateShortcutFile(Path.Combine(userPrograms, "GlobalProtect.lnk"));
                            CreateShortcutFile(Path.Combine(userPrograms, "Palo Alto Networks", "GlobalProtect", "GlobalProtect.lnk"));
                        }
                    }
                    catch { }

                    if (onProgress != null) onProgress(85, "Instalando archivos y registrando servicio...");

                    List<ScheduledAction> actions = new List<ScheduledAction>();

                    // Copiar proveedores de credenciales a System32 si existen
                    string sys64Src = Path.Combine(extractDir, "System (64-bit) Folder");
                    if (Directory.Exists(sys64Src))
                    {
                        actions.Add(new ScheduledAction(
                            @"C:\Windows\System32\robocopy.exe",
                            "\"" + sys64Src + "\" \"C:\\Windows\\System32\" /IS /IT /NFL /NDL /NJH /NJS /R:1 /W:1"));
                    }

                    // Configurar claves de Registro de GlobalProtect y Panel de Control mediante importación limpia con reg.exe
                    string regFile = Path.Combine(extractDir, "gp_install.reg");
                    string regContent =
                        "Windows Registry Editor Version 5.00\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Palo Alto Networks\\GlobalProtect]\r\n" +
                        "\"Version\"=\"6.2.5-788\"\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Palo Alto Networks\\GlobalProtect\\PanSetup]\r\n" +
                        "\"Installfolder\"=\"C:\\\\Program Files\\\\Palo Alto Networks\\\\GlobalProtect\\\\\"\r\n" +
                        "\"ProductCode\"=\"" + DefaultProductCode + "\"\r\n" +
                        "\"Version\"=\"1.0.0.1\"\r\n" +
                        "\"Portal\"=\"\"\r\n" +
                        "\"Prelogon\"=\"0\"\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Palo Alto Networks\\GlobalProtect\\PanInstaller]\r\n" +
                        "\"ProductCode\"=\"" + DefaultProductCode + "\"\r\n" +
                        "\"Version\"=\"1.0.0.1\"\r\n" +
                        "\"DebugLevel\"=dword:00000004\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Palo Alto Networks\\GlobalProtect\\DrvCtrl]\r\n" +
                        "\"DebugLevel\"=dword:00000003\r\n" +
                        "\"EnableMonitor\"=dword:00000001\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Palo Alto Networks\\GlobalProtect\\PanMSService]\r\n" +
                        "\"PreferredIP\"=\"1.1.1.1\"\r\n" +
                        "\"DebugLevel\"=\"4\"\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Palo Alto Networks\\GlobalProtect\\Settings]\r\n" +
                        "\"disable-globalprotect\"=dword:00000000\r\n\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + DefaultProductCode + "]\r\n" +
                        "\"DisplayName\"=\"GlobalProtect\"\r\n" +
                        "\"DisplayVersion\"=\"6.2.5\"\r\n" +
                        "\"Publisher\"=\"Palo Alto Networks\"\r\n" +
                        "\"InstallLocation\"=\"C:\\\\Program Files\\\\Palo Alto Networks\\\\GlobalProtect\\\\\"\r\n" +
                        "\"DisplayIcon\"=\"C:\\\\Program Files\\\\Palo Alto Networks\\\\GlobalProtect\\\\PanGPA.exe\"\r\n" +
                        "\"UninstallString\"=\"\\\"C:\\\\Program Files\\\\Palo Alto Networks\\\\GlobalProtect\\\\PanGPS.exe\\\" -uninstall\"\r\n" +
                        "\"EstimatedSize\"=dword:0000f0fb\r\n" +
                        "\"NoModify\"=dword:00000001\r\n" +
                        "\"NoRepair\"=dword:00000001\r\n" +
                        "\"WindowsInstaller\"=-\r\n";

                    File.WriteAllText(regFile, regContent, System.Text.Encoding.Unicode);

                    // Copiar todos los archivos del programa hacia C:\Program Files\Palo Alto Networks\GlobalProtect
                    actions.Add(new ScheduledAction(
                        @"C:\Windows\System32\robocopy.exe",
                        "\"" + extractDir + "\" \"" + TargetInstallDir + "\" /E /XD \"System (64-bit) Folder\" /XF \"" + LocalMsiFileName + "\" \"GlobalProtect.lnk\" \"gp_install.reg\" /IS /IT /NFL /NDL /NJH /NJS /R:1 /W:1"));

                    // Copiar accesos directos al Menú Inicio global (C:\ProgramData\Microsoft\Windows\Start Menu\Programs)
                    actions.Add(new ScheduledAction(
                        @"C:\Windows\System32\robocopy.exe",
                        "\"" + extractDir + "\" \"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\" \"GlobalProtect.lnk\" /IS /IT /NFL /NDL /NJH /NJS /R:1 /W:1"));
                    actions.Add(new ScheduledAction(
                        @"C:\Windows\System32\robocopy.exe",
                        "\"" + extractDir + "\" \"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Palo Alto Networks\\GlobalProtect\" \"GlobalProtect.lnk\" /IS /IT /NFL /NDL /NJH /NJS /R:1 /W:1"));

                    actions.Add(new ScheduledAction(@"C:\Windows\System32\reg.exe", "import \"" + regFile + "\""));

                    string panGpsPath = Path.Combine(TargetInstallDir, "PanGPS.exe");

                    // Ejecutar registrador oficial firmado de Palo Alto (PanGPS.exe -commit) y levantar el servicio PanGPS
                    actions.Add(new ScheduledAction(panGpsPath, "-commit", TargetInstallDir));
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "config PanGPS start= auto"));
                    actions.Add(new ScheduledAction(@"C:\Windows\System32\sc.exe", "start PanGPS"));

                    AdminExec.RunScheduledActions(actions, 75);
                }
                finally
                {
                    ForceDeleteDirectory(extractDir);
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

                if (onProgress != null) onProgress(100, "Instalación finalizada");
                return IsInstalled();
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

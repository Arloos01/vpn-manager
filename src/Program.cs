using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;

namespace VPNTool
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                PortablePaths.Initialize();

                // Si por alguna razón el proceso arrancó sin privilegios de administrador,
                // solicitar elevación nativa inmediata para garantizar permisos completos.
                if (!AdminExec.IsCurrentProcessElevated())
                {
                    try
                    {
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        return;
                    }
                    catch
                    {
                        // Si el usuario cancela la elevación, permitir continuar
                    }
                }

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(PortablePaths.LogsDir, "domain_crash.txt"), e.ExceptionObject.ToString());
                    }
                    catch { }
                };

                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | (SecurityProtocolType)768 | (SecurityProtocolType)192;
                    ServicePointManager.Expect100Continue = true;
                }
                catch { }

                Application app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(PortablePaths.LogsDir, "dispatcher_crash.txt"), e.Exception.ToString());
                    }
                    catch { }
                    e.Handled = false;
                };

                MainWindow window = new MainWindow();
                app.Run(window);

                PortablePaths.CleanTempDirectory();
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(PortablePaths.LogsDir, "main_crash.txt"), ex.ToString());
                }
                catch { }
            }
        }
    }
}

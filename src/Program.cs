using System;
using System.Collections.Generic;
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

                // Proceso auxiliar headless para ejecución de acciones con privilegios elevados (UAC fallback)
                if (args != null && args.Length >= 2 && args[0].Equals("--exec-job", StringComparison.OrdinalIgnoreCase))
                {
                    int exitCode = ExecuteJobHeadless(args[1]);
                    Environment.Exit(exitCode);
                    return;
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

        private static int ExecuteJobHeadless(string jobFile)
        {
            try
            {
                List<ScheduledAction> actions = AdminExec.LoadActionsFromFile(jobFile);
                if (actions == null || actions.Count == 0) return 1;

                bool ok = AdminExec.RunActionsDirect(actions);
                return ok ? 0 : 2;
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(PortablePaths.LogsDir, "exec_job_error.txt"), ex.ToString());
                }
                catch { }
                return 3;
            }
        }
    }
}

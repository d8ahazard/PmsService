using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using PlexServiceCommon;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace PlexServiceTray
{
    public static class Launch
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
            var logPath = Path.Combine(PlexDirHelper.AppDataPath, "PlexServiceTray.log");

            var lc = new LoggerConfiguration()
                .Enrich.WithCaller()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
                //.Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
                .Enrich.FromLogContext()
                .WriteTo.Async(a =>
                    a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate));

			
            Log.Logger = lc.CreateLogger();
            var appProcessName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            var runningProcesses = Process.GetProcessesByName(appProcessName);
            if (runningProcesses.Length > 1) {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                var applicationContext = new NotifyIconApplicationContext();
                Application.Run(applicationContext);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Program Terminated Unexpectedly", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

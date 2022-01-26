using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using PlexServiceCommon;
using PlexServiceCommon.Logging;
using Serilog;

namespace PlexServiceTray
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App {
        [STAThread]

        public void Application_Startup(object sender, StartupEventArgs e)
        {
            const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
            var logPath = Path.Combine(LogWriter.AppDataPath, "PlexServiceTray.log");
            Console.WriteLine("Logging to " + logPath);
            var lc = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithCaller()
                //.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
                .Enrich.FromLogContext()
                .WriteTo.Async(a =>
                    a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate));

			
            Log.Logger = lc.CreateLogger();
            //Log.CloseAndFlush();
            var app = Process.GetCurrentProcess().MainModule;
            if (app != null) {
                var file = app.FileName;
                var appProcessName = Path.GetFileNameWithoutExtension(file);
                var runningProcesses = Process.GetProcessesByName(appProcessName);
                if (runningProcesses.Length > 1) {
                    return;
                }
            }
            
            try
            {
                var applicationContext = new NotifyIconApplicationContext();
                applicationContext.Connect().ConfigureAwait(false);
            }
            catch (Exception)
            {
                //
            }
        }
    }
}

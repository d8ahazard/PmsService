using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using PlexServiceCommon;
using PlexServiceCommon.Logging;
using PlexServiceTray;
using Serilog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

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
            Log.Debug("WTF??");
            //Console.WriteLine("No, seriously.");

            // var appProcessName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            // var runningProcesses = Process.GetProcessesByName(appProcessName);
            // if (runningProcesses.Length > 1) {
            //     return;
            // }
            try
            {
                Log.Debug("Logging works...");
                var applicationContext = new NotifyIconApplicationContext();
                applicationContext.Connect().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                //
            }
        }
    }
}

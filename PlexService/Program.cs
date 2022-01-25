using System.IO;
using PlexServiceCommon;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlexService.Models;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace PlexService
{
	public static class Program {
		public static void Main(string[] args) {
			const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
			var logPath = Path.Combine(PlexDirHelper.AppDataPath, "PMSS.log");

			var lc = new LoggerConfiguration()
				.Enrich.WithCaller()
				.MinimumLevel.Debug()
				.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
				//.Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
				.Enrich.FromLogContext()
				.WriteTo.Async(a =>
					a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate));

			
			Log.Logger = lc.CreateLogger();
			CreateHostBuilder(args, Log.Logger).Build().Run();
			Log.Debug("Host builder done.");
			Log.CloseAndFlush();
		}
		
		private static IHostBuilder CreateHostBuilder(string[] args, ILogger logger) {
			Log.Debug("Creating host builder.");
			var settings = SettingsHandler.Load();
			var url = "http://localhost:" + settings.ServerPort;
			return Host.CreateDefaultBuilder(args)
				.UseSerilog(logger)
				.UseWindowsService()
				.ConfigureServices(services => {
					services.AddSingleton<IHostedService, PlexMediaServerService>();
					services.AddSignalR();
				})
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseKestrel((_, kestrelOptions) =>
					{
						kestrelOptions.ListenAnyIP(settings.ServerPort);
					});
					webBuilder.UseUrls(url);
					webBuilder.UseStartup<Startup>();
				});
		}
    }
}

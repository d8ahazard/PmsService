﻿#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace PlexServiceCommon
{
    /// <summary>
    /// Static class for writing to the log file
    /// </summary>
    public static class LogWriter {
        private static ILogger? _log;
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");
        public static string LogFile {
            get {
                var dt = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                var logPath = Path.Combine(AppDataPath, $"PlexService{dt}.log");
                return logPath;
            }
        }

        public static void Init() {
            if (_log != null) return;
            if (!Directory.Exists(AppDataPath)) {
                Directory.CreateDirectory(AppDataPath);
            }
            const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
            var lc = new LoggerConfiguration()
                .Enrich.WithCaller()
                .MinimumLevel.Debug()
                .Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
                .Enrich.FromLogContext()
                .WriteTo.Async(a =>
                    a.File(Path.Combine(AppDataPath, "PlexService.log"),shared: true, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate));

            
            Log.Logger = lc.CreateLogger();
            _log = Log.Logger;
        }

        public static async Task<string> Read() {
            var log = string.Empty;
            if (!File.Exists(LogFile)) {
                Log.Debug("No logfile found??");
                return log;
            }

            var count = 0;
            Log.Debug("Reading log to client...");
            while (count < 10) {
                try {
                    Stream stream = File.Open(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var streamReader = new StreamReader(stream);
                    var str = await streamReader.ReadToEndAsync();
                    log = str;
                    streamReader.Close();
                    stream.Close();
                    Log.Debug("Log, read " + log.Length + " chars.");
                    return log;
                } catch (Exception ex) {
                    Log.Warning($"Can't read from self ({count}): " + ex.Message);
                }

                await Task.Delay(500);
                count++;
            }
            
            return log;
        }
    }
}

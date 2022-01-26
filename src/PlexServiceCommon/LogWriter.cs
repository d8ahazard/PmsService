#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace PlexServiceCommon
{
    /// <summary>
    /// Static class for writing to the log file
    /// </summary>
    public static class LogWriter {
        public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");
        

        
        public static async Task<string> Read() {
            var log = string.Empty;
            if (!File.Exists(PlexDirHelper.LogFile)) {
                Log.Debug("No logfile found??");
                return log;
            }

            var count = 0;
            Log.Debug("Reading log to client...");
            while (count < 10) {
                try {
                    Stream stream = File.Open(PlexDirHelper.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

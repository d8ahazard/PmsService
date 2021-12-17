﻿using System.IO;
using Newtonsoft.Json;
using PlexServiceCommon;

namespace PlexService.Models
{
    /// <summary>
    /// Class for loading and saving settings on the server
    /// Code is here rather than in the settings class as it should only ever be save on the server.
    /// settings are retrieved remotely by calling the wcf service GetSettings and SetSettings methods
    /// </summary>
    public static class SettingsHandler
    {
        #region Load/Save

        private static string GetSettingsFile()
        {
            return Path.Combine(PlexDirHelper.AppDataPath, "Settings.json");
        }

        /// <summary>
        /// Save the settings file
        /// </summary>
        internal static void Save(Settings settings)
        {
            var filePath = GetSettingsFile();

            if (!Directory.Exists(Path.GetDirectoryName(filePath))) {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }

            using var sw = new StreamWriter(filePath, false);
            sw.Write(JsonConvert.SerializeObject(settings, Formatting.Indented));
            // #TODO: Add callback for settings change to clients
        }


        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        public static Settings Load()
        {
            var filePath = GetSettingsFile();
            Settings settings;
            if (File.Exists(filePath)) {
                using var sr = new StreamReader(filePath);
                var rawSettings = sr.ReadToEnd();
                settings = JsonConvert.DeserializeObject<Settings>(rawSettings);
                //this new setting may not be in the settings file for users of the old app who have a settings file already.
                //we need a value or bang...
                if (string.IsNullOrEmpty(settings.Theme))
                    settings.Theme = "Dark.Red";
            }
            else
            {
                settings = new Settings();
                Save(settings);
            }
            return settings;
        }

        #endregion
    }
}

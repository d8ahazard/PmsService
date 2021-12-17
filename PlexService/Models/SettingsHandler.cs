using System.IO;
using Newtonsoft.Json;
using PlexServiceCommon;

namespace PlexService.Models
{
    /// <summary>
    /// Class for loading and saving settings on the server
    /// Code is here rather than in the settings class as it should only ever be save on the server.
    /// settings are retrieved remotely by calling the wcf service GetSettings and SetSettings methods
    /// </summary>
    public static class SettingsHandler {
        private static Settings? _settings;
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
            _settings = settings;
        }


        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        public static Settings Load() {
            if (_settings != null) return _settings;
            var filePath = GetSettingsFile();
            Settings? settings = null;
            if (File.Exists(filePath)) {
                using var sr = new StreamReader(filePath);
                var rawSettings = sr.ReadToEnd();
                settings = JsonConvert.DeserializeObject<Settings>(rawSettings);
            }

            if (settings != null) {
                if (string.IsNullOrEmpty(settings.Theme)) settings.Theme = "Dark.Red";
                _settings = settings;
            }
            else
            {
                settings = new Settings();
                Save(settings);
                _settings = settings;
            }
            return _settings;
        }

        #endregion
    }
}

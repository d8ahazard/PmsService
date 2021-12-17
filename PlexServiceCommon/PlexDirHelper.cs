using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace PlexServiceCommon {
	public static class PlexDirHelper {
		
		public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");

		public static readonly string PmsDataPath = GetPlexDataDir();
		
		public static string LogFile {
			get {
				var dt = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
				var logPath = Path.Combine(AppDataPath, $"PlexService{dt}.log");
				return logPath;
			}
		}

		/// <summary>
		/// Returns the full path and filename of the plex media server executable
		/// </summary>
		/// <returns></returns>
		private static string GetPlexDataDir()
		{
			var result = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var path = Path.Combine(result, "Plex Media Server");
			if (Directory.Exists(path)) {
				return path;
			}
			result = string.Empty;
			var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

			var architecture = is64Bit ? RegistryView.Registry64 : RegistryView.Registry32;

			using var pmsDataKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, architecture)
				.OpenSubKey(@"Software\Plex, Inc.\Plex Media Server");
			if (pmsDataKey == null) {
				return result;
			}

			path = (string) pmsDataKey.GetValue("LocalAppdataPath");
			result = path;

			return result;
		}
	}
}
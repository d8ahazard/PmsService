#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Win32;
using PlexService.Hubs;
using PlexServiceCommon;
using Serilog;

namespace PlexService.Models
{
    /// <summary>
    /// Class for monitoring the status of Plex Media Server
    /// </summary>
    public class PmsMonitor
    {
        #region static strings
        private readonly IHubContext<SocketServer> _hubContext;
        //Process names
        private static readonly string _plexName = "Plex Media Server";
        //List of processes spawned by plex that we need to get rid of
        private static readonly string[] SupportingProcesses =
        {
            "Plex DLNA Server",
            "PlexScriptHost",
            "PlexTranscoder",
            "PlexNewTranscoder",
            "Plex Media Scanner",
            "PlexRelay",
            "Plex Relay",
            "EasyAudioEncoder",
            "Plex Tuner Service"
        };

        #endregion

        #region Private variables

        /// <summary>
        /// The name of the plex media server executable
        /// </summary>
        private string _executableFileName = string.Empty;

        /// <summary>
        /// Plex process
        /// </summary>
        private Process? _plex;

        /// <summary>
        /// Flag to determine if PMS is updating itself.
        /// </summary>
        private bool _updating;

        public List<AuxiliaryApplicationMonitor> AuxAppMonitors { get; }

        #endregion

        #region Properties

        private PlexState _state = PlexState.Unknown;

        public PlexState State
        {
            get => _state;
            private set {
                if (_state == value) {
                    return;
                }

                _state = value;
                OnStateChange();
            }
        }

        #endregion

        #region Constructor

        internal PmsMonitor(IHubContext<SocketServer> hubContext) {
            _hubContext = hubContext;
            State = PlexState.Stopped;
            AuxAppMonitors = new List<AuxiliaryApplicationMonitor>();
            var settings = SettingsHandler.Load();
            settings.AuxiliaryApplications.ForEach(x => AuxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
            Start();
            WatchLog();
        }
        #endregion

        #region PurgeAutoStart

        /// <summary>
        /// This method will look for and remove the "run at startup" registry key for plex media server.
        /// </summary>
        /// <returns></returns>
        private void PurgeAutoStartRegistryEntry()
        {
            const string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
            if (key?.GetValue("Plex Media Server") == null) {
                return;
            }

            try
            {
                key.DeleteValue("Plex Media Server");
                Log.Information("Successfully removed auto start entry from registry");
            }
            catch(Exception ex)
            {
                Log.Warning($"Unable to remove auto start registry value. Error: {ex.Message}");
            }
        }

        #endregion

        #region DisableFirstRun

        /// <summary>
        /// This method will set the "FirstRun" registry key to 0 to prevent PMS from spawning the default browser.
        /// </summary>
        /// <returns></returns>
        private void DisableFirstRun() {
            RegistryKey? pmsDataKey = null;
            const string keyName = @"Software\Plex, Inc.\Plex Media Server";
            var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

            var architecture = is64Bit ? RegistryView.Registry64 : RegistryView.Registry32;
            try {
                pmsDataKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, architecture).OpenSubKey(keyName);
                if (pmsDataKey != null) {
                    var firstRun = (int) (pmsDataKey.GetValue("FirstRun") ?? 1);
                    Log.Debug("First run is: " + firstRun);
                    if (firstRun == 0) return;
                }
            } catch (Exception e) {
                Log.Warning("Exception getting pms key: " + e.Message);
            }

            // CreateSubKey just in case it isn't already there for some reason.
            // The installer adds values under here during install, but this can't hurt.
            if (pmsDataKey == null) {
                using var key = Registry.CurrentUser.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (key == null) {
                    return;
                }

                pmsDataKey = key;
            }
            
            try
            {
                pmsDataKey.SetValue("FirstRun", 0, RegistryValueKind.DWord);
                Log.Information("Successfully set the 'FirstRun' registry key to 0");
            }
            catch(Exception ex)
            {
                Log.Information($"Unable to set the 'FirstRun' registry key to 0. Error: {ex.Message}");
            }
        }

        #endregion

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        public void Start()
        {
            //Find the plex executable
            _executableFileName = GetPlexExecutable();
            if (string.IsNullOrEmpty(_executableFileName))
            {
                Log.Information("Plex Media Server does not appear to be installed!");
                State = PlexState.Stopped;
            }
            else
            {
                //load the settings
                var settings = SettingsHandler.Load();

                Log.Information("Plex executable found at " + _executableFileName);
                
                //map network drives
                var drivesMapped = true;
                if (settings.DriveMaps.Count > 0) {
                    Log.Information("Mapping Network Drives");

                    drivesMapped = settings.DriveMaps.All(toMap => TryMap(toMap, settings));
                }

                if (!drivesMapped && !settings.StartPlexOnMountFail) 
                {
                    Log.Warning("One or more drive mappings failed and settings are configured to *not* start Plex on mount failure. Please check your drives and try again.");
                } 
                else 
                {
                    StartPlex();
                }
                
                //stop any running aux apps
                AuxAppMonitors.ForEach(a => a.Stop());
                AuxAppMonitors.Clear();
                settings.AuxiliaryApplications.ForEach(x => AuxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
                //hook up the state change event for all the applications
                AuxAppMonitors.AsParallel().ForAll(x => x.Start());
            }
        }

        private void WatchLog() {
            var path = PlexDirHelper.PmsDataPath;
            
            if (string.IsNullOrEmpty(path)) return;

            path = Path.Combine(path, "PLex Media Server" , "Logs");
            Log.Debug("PMS Log Path: " + path);
            try 
            {
                var watcher = new FileSystemWatcher(path,"Plex Update Service Launcher.log");
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.EnableRaisingEvents = true;
                watcher.Changed += OnChanged;
            }
            catch (Exception e) 
            {
                Log.Warning("Exception: " + e.Message);
            }
        }
        
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            var read = false;
            var lastLine = string.Empty;
            // Ensure the file isn't in use when we try to read it.
            while (!read) 
            {
                try 
                {
                    lastLine = File.ReadLines(e.FullPath).ToArray().Last();
                    read = true;
                } 
                catch (Exception) 
                {
                    // Ignored, we know what the problem is
                }
            }
            // Only set _updating once.
            if (lastLine != null && lastLine.Contains("Closing Plex Media Server Processes") && !_updating) 
            {
                Log.Debug("MATCH");
                State = PlexState.Updating;
                Log.Information("Plex update started.");
                _updating = true;
                return;
            }

            // And only unset it if it's already been set.
            if (lastLine != null && (!lastLine.Contains("Install: Success") || !_updating)) 
            {
                return;
            }
            _updating = false;
            Log.Information("PMS update is complete, killing and restarting process.");
            Start();
        }

        private static bool TryMap(DriveMap map, Settings settings)
        {
            var count = settings.AutoRemount ? settings.AutoRemountCount : 1;

            while (count > 0)
            {
                try
                {
                    map.MapDrive(true);
                    Log.Information($"Map share {map.ShareName} to letter '{map.DriveLetter}' successful");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Information($"Unable to map share {map.ShareName} to letter '{map.DriveLetter}': {ex.Message}, {count - 1} more attempts remaining.");
                }
                // Wait 5s
                Thread.Sleep(settings.AutoRemountDelay * 1000);
                count--;
            }

            return false;
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stop the monitor and kill the processes
        /// </summary>
        internal void Stop()
        {
            State = PlexState.Stopping;
            EndPlex();
            KillSupportingProcesses();
        }

        #endregion

        #region Restart

        /// <summary>
        /// Restart plex, wait for the specified delay between stop and start
        /// </summary>
        /// <param name="delay">The amount of time in ms to wait before starting after stop</param>
        internal void Restart(int delay)
        {
            Stop();
            State = PlexState.Pending;
            var autoEvent = new AutoResetEvent(false);
            var t = new Timer(_ => { Start(); autoEvent.Set(); }, null, delay, Timeout.Infinite);
            autoEvent.WaitOne();
            t.Dispose();
        }

        #endregion

        #region Process handling

        #region Exit events

        /// <summary>
        /// This event fires when the plex process we have a reference to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Plex_Exited(object? sender, EventArgs e)
        {
            Log.Information("Plex Media Server has stopped!");
            //unsubscribe
            if (_plex != null) _plex.Exited -= Plex_Exited;

            //kill the supporting processes.
            KillSupportingProcesses();

            if (_plex != null)
            {
                _plex.Dispose();
                _plex = null;
            }

            //restart as required
            var settings = SettingsHandler.Load();
            if (State != PlexState.Stopping && settings.AutoRestart)
            {
                if (_updating) {
                    State = PlexState.Stopped;
                    Log.Information("Plex is updating, waiting for finish before re-starting.");
                    return;
                }
                Log.Information($"Waiting {settings.RestartDelay} seconds before re-starting the Plex process.");
                State = PlexState.Pending;
                var autoEvent = new AutoResetEvent(false);
                var t = new Timer(_ => { Start(); autoEvent.Set(); }, null, settings.RestartDelay * 1000, Timeout.Infinite);
                autoEvent.WaitOne();
                t.Dispose();
            }
            else
            {
                //set the status
                State = PlexState.Stopped;
            }
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new/get a handle on existing Plex process
        /// </summary>
        private void StartPlex()
        {
            State = PlexState.Pending;
            //always try to get rid of the plex auto start registry entry
            PurgeAutoStartRegistryEntry();
            // make sure we don't spawn a browser
            Log.Debug("Disabling first run.");
            DisableFirstRun(); 
            if (_plex == null)
            {
                Log.Debug("No plex defined, checking for running process.");
                //see if its running already
                _plex = Process.GetProcessesByName(_plexName).FirstOrDefault();
                if (_plex != null) {
                    Log.Information("Killing existing Plex service.");
                    _plex.Kill(true);
                    KillSupportingProcesses();
                    _plex = null;
                }
                
                if (_plex == null)
                {
                    Log.Information("Attempting to start Plex.");
                    //plex process
                    _plex = new Process();
                    var plexStartInfo = new ProcessStartInfo(_executableFileName) {
                        WorkingDirectory = Path.GetDirectoryName(_executableFileName) ?? string.Empty,
                        UseShellExecute = false
                    };
                    //check version to see if we can use the startup argument
                    var plexVersion = FileVersionInfo.GetVersionInfo(_executableFileName).FileVersion;
                    var v = new Version(plexVersion ?? string.Empty);
                    var minimumVersion = new Version("0.9.8.12");
                    if (v.CompareTo(minimumVersion) == -1)
                    {
                        Log.Information($"Plex Media Server version is {plexVersion}. Cannot use startup argument.");
                    }
                    else
                    {
                        Log.Information($"Plex Media Server version is {plexVersion}. Can use startup argument.");
                        plexStartInfo.Arguments = "-noninteractive";
                    }
                    _plex.StartInfo = plexStartInfo;
                    _plex.EnableRaisingEvents = true;
                    _plex.Exited += Plex_Exited;
                    try
                    {
                        if (_plex.Start()) {
                            State = PlexState.Running;
                            Log.Information("Plex Media Server Started.");    
                        }
                    }
                    catch(Exception ex)
                    {
                        Log.Warning("Plex Media Server failed to start. " + ex.Message);
                    }
                }
            }
            //set the state back to stopped if we didn't achieve a running state
            if (State != PlexState.Running)
                State = PlexState.Stopped;
        }

        #endregion

        #region End methods

        /// <summary>
        /// Kill the plex process
        /// </summary>
        private void EndPlex()
        {
            if (_plex != null)
            {
                Log.Information("Killing Plex.");
                try
                {
                    _plex.Kill();
                } catch (Exception e) {
                    Log.Warning("Exception killing Plex: " + e.Message);
                }
            }
            //kill each auxiliary process
            AuxAppMonitors.ForEach(appMonitor =>
            {
                appMonitor.Stop();
            });
        }

        /// <summary>
        /// Kill all processes with the specified names
        /// </summary>
        private static void KillSupportingProcesses()
        {
            Log.Information("Killing supporting processes.");
            foreach (var name in SupportingProcesses)
            {
                KillSupportingProcess(name);
            }
        }

        /// <summary>
        /// Kill all instances of the specified process.
        /// </summary>
        /// <param name="name">The name of the process to kill</param>
        private static void KillSupportingProcess(string name)
        {
            //see if its running
            Log.Information("Looking for process: " + name);
            var supportProcesses = Process.GetProcessesByName(name);
            Log.Information(supportProcesses.Length + " instances of " + name + " found.");
            if (supportProcesses.Length <= 0) {
                return;
            }

            foreach (var supportProcess in supportProcesses)
            {
                Log.Information($"Stopping {name} with PID of {supportProcess.Id}.");
                try
                {
                    supportProcess.Kill();
                    Log.Information(name + " with PID stopped");
                }
                catch
                {
                    Log.Warning("Unable to stop process " + supportProcess.Id);
                }
                finally
                {
                    supportProcess.Dispose();
                }
            }
        }

        #endregion

        #endregion

        #region Aux app interaction methods

        public bool IsAuxAppRunning(string name)
        {
            var auxApp = AuxAppMonitors.FirstOrDefault(a => a.Name == name);
            return auxApp is { Running: true };
        }

        public bool StartAuxApp(string name)
        {
            var auxApp = AuxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp is { Running: false }) auxApp.Start();
            return auxApp?.Running ?? false;
        }

        public bool StopAuxApp(string name)
        {
            var auxApp = AuxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp is { Running: true }) auxApp.Stop();
            return !auxApp?.Running ?? true;
        }

        #endregion

        #region File Methods

        /// <summary>
        /// Returns the full path and filename of the plex media server executable
        /// </summary>
        /// <returns></returns>
        private static string GetPlexExecutable() {
            var result = string.Empty;

            //first we will do a dirty check for a text file with the executable path in our log folder.
            //this is here to help anyone having issues and let them specify it manually themselves.
            var locationFile = Path.Combine(PlexDirHelper.AppDataPath, "location.txt");

            if(!string.IsNullOrEmpty(PlexDirHelper.AppDataPath))
            {
                if (File.Exists(locationFile))
                {
                    string userSpecified;
                    using (var sr = new StreamReader(locationFile))
                    {
                        userSpecified = sr.ReadLine() ?? string.Empty;
                    }
                    if (File.Exists(userSpecified))
                    {
                        result = userSpecified;
                    }
                }
            }

            //if theres nothing there go for the easy defaults
            if (string.IsNullOrEmpty(result)) {
                //plex doesn't put this nice stuff in the registry so we need to go hunting for it ourselves
                //this method is crap. I dont like having to iterate through directories looking to see if a file exists or not.
                //start by looking in the program files directory, even if we are on 64bit windows, plex may be 64bit one day... maybe

                var possibleLocations = new List<string> {
                    //some hard coded attempts, this is nice and fast and should hit 90% of the time... even if it is ugly
                    @"C:\Program Files\Plex\Plex Media Server\Plex Media Server.exe",
                    @"C:\Program Files (x86)\Plex\Plex Media Server\Plex Media Server.exe",
                    //special folder
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Plex\Plex Media Server\Plex Media Server.exe")
                };


                foreach (var location in possibleLocations.Where(File.Exists)) {
                    File.WriteAllText(locationFile, location);
                    return location;
                }
            }

            //work out the os type (32 or 64) and set the registry view to suit. this is only a reliable check when this project is compiled to x86.
            var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

            var architecture = RegistryView.Registry32;
            if (is64Bit)
            {
                architecture = RegistryView.Registry64;
            }

            using var userDataKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, architecture).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Installer\UserData");
            if (userDataKey == null) {
                return result;
            }

            foreach (var userKeyName in userDataKey.GetSubKeyNames()) {
                using var userKey = userDataKey.OpenSubKey(userKeyName);
                using var componentsKey = userKey?.OpenSubKey("Components");
                if (componentsKey == null) {
                    continue;
                }

                foreach (var guidKeyName in componentsKey.GetSubKeyNames()) {
                    using var guidKey = componentsKey.OpenSubKey(guidKeyName);
                    if (guidKey == null) {
                        continue;
                    }

                    foreach (var valueName in guidKey.GetValueNames()) {
                        var value = guidKey.GetValue(valueName)?.ToString() ?? string.Empty;
                        if (!value.ToLower().Contains("plex media server.exe")) {
                            continue;
                        }

                        //found it hooray!
                        File.WriteAllText(locationFile, value);
                        return value;
                    }
                }
            }
            
            // Last but not least, try scanning *everything*
            result = FindPlexExecutable();
            if (!string.IsNullOrEmpty(result)) File.WriteAllText(locationFile, result);
            return result;
        }

        private static string FindPlexExecutable() {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives) {
                if (drive.DriveType == DriveType.Fixed) {
                    Log.Debug("Looking in drive " + drive.Name);
                    var tgt = Scan(drive.RootDirectory.ToString());
                    if (!string.IsNullOrEmpty(tgt)) return tgt;
                }
            }

            return string.Empty;
        }

        private static string Scan(string path) {
            try {
                foreach (var file in Directory.EnumerateFiles(path,"Plex Media Server.exe")) {
                    return file;
                }
            } catch {
                // Ignored
            }

            try {
                foreach (var dir in Directory.EnumerateDirectories(path)) {
                    var res = Scan(dir);
                    if (!string.IsNullOrEmpty(res)) return res;
                }
            } catch {
                // Ignored
            }

            return string.Empty;
        }
        
        
        #endregion

        #region StateChange

        
        private void OnStateChange() {
            _hubContext.Clients.All.SendAsync("state", _state);
        }

        #endregion
    }
}

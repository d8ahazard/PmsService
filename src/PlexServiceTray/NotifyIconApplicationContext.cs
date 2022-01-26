#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using PlexServiceCommon;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Serilog.Events;

namespace PlexServiceTray
{
    /// <summary>
    /// Tray icon context
    /// </summary>
    internal class NotifyIconApplicationContext : ApplicationContext
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private readonly IContainer _components;
        private readonly NotifyIcon _notifyIcon;
        private readonly HubConnection _connection;
        private SettingsWindow? _settingsWindow;
        private ConnectionSettingsWindow? _connectionSettingsWindow;
        private Settings? _settings;
        private PlexState _state = PlexState.Unknown;
        private string _pmsPath = string.Empty;
        private string _logPath = string.Empty;
        private readonly ConnectionSettings _connectionSettings;
        private Dictionary<string, bool> _states;
        private SettingsWindowViewModel? _viewModel;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _components != null)
            {
                Disconnect();
                _components.Dispose();
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        public NotifyIconApplicationContext() {
            _states = new Dictionary<string, bool>();
            // Moved directly to constructor to suppress nullable warnings.
            _components = new Container();
            var assy = Environment.ProcessPath;
            var appIcon = Icon.ExtractAssociatedIcon(assy);
            _notifyIcon = new NotifyIcon(_components)
            {
                ContextMenuStrip = new ContextMenuStrip
                {
                    ForeColor = Color.FromArgb(232, 234, 237),
                    BackColor = Color.FromArgb(41, 42, 45),
                    RenderMode = ToolStripRenderMode.System,
                    DropShadowEnabled = true,
                    AutoSize = true
                },
                Icon = appIcon,
                Text = "Manage Plex Media Server Service",
                Visible = true
            };
            _notifyIcon.MouseClick += NotifyIcon_Click;
            _notifyIcon.MouseDoubleClick += NotifyIcon_DoubleClick;
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            _connectionSettings = ConnectionSettings.Load();
            var url = $"http://{_connectionSettings.ServerAddress}:{_connectionSettings.ServerPort}/socket";
            _connection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();
            _connection.KeepAliveInterval = TimeSpan.FromMilliseconds(500);
            _connection.Closed += Callback_ConnectionChanged;
            _connection.Reconnecting += Callback_ConnectionChanged;
            _connection.Reconnected += Callback_Reconnected;
                    
            _connection.On<Settings>("settings", Callback_SettingChange);
            _connection.On<PlexState>("state", Callback_StateChange);
            _connection.On<string>("pmsPath", Callback_PmsPathChange);
            _connection.On<string>("logPath", Callback_PmsLogChange);
            _connection.On<Dictionary<string, bool>>("states", Callback_States);
            _connection.On<Tuple<string, bool>>("auxState", Callback_AuxState);
            Connect().ConfigureAwait(true);
            DrawMenu().ConfigureAwait(true);
        }

        private async Task Callback_AuxState(Tuple<string, bool> state) {
            var (item1, item2) = state;
            _states[item1] = item2;
            await DrawMenu();
            var sString = item2 ? "started" : "stopped";
            _notifyIcon.ShowBalloonTip(1000, "Plex Service", $"{item1} {sString}.", ToolTipIcon.Info);
        }

        private async Task Callback_States(Dictionary<string, bool> states) {
            _states = states;
            await DrawMenu();
        }


        /// <summary>
        /// Connect to Websocket
        /// </summary>
        public async Task Connect()
        {
            if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting) {
                Log.Debug("Already connected or connecting..." + _connection.State);
                return;
            }
            Log.Debug("Connect called...");
            if (_connection != null) {
                try {
                    Log.Debug("Connecting to client.");
                    await _connection.StartAsync();
                    if (_connection.State == HubConnectionState.Connected) {
                        Log.Debug("Connected??");
                    } else {
                        Log.Debug("Not connected? " + _connection.State);
                    }
                    
                    Log.Debug("Connection configured.");
                } catch (Exception e) {
                    Log.Debug("Connection error: " + e.Message);
                }
            } else {
                Log.Debug("CONNECTION IS NULL!");
            }
            
        }

        private void Callback_PmsPathChange(string obj) {
            _pmsPath = obj;
        }

        private void Callback_PmsLogChange(string obj) {
            _logPath = obj;
        }

        private async Task Callback_Reconnected(string? arg) {
            Log.Debug("Reconnected!!");
            await DrawMenu();
        }

        private async Task Callback_ConnectionChanged(Exception? arg) {
            Log.Warning("Connection state change! " + _connection.State);
            await DrawMenu();
        }

        private async Task Callback_StateChange(PlexState description) {
            _state = description;
            Log.Debug("State change fired: " + description);
            await DrawMenu();
            _notifyIcon.ShowBalloonTip(2000, "Plex Service", PlexStateString(), ToolTipIcon.Info);
        }

        private async Task Callback_SettingChange(Settings settings) {
            _settings = settings;
            if (_viewModel != null) {
                _viewModel.WorkingSettings = _settings;
            }
            await DrawMenu();
            Logger("Settings update fired.");
        }

        /// <summary>
        /// Disconnect from websocket
        /// </summary>
        private void Disconnect()
        {
            Log.Debug("Disconnecting??");
            try {
                _connection.StopAsync().ConfigureAwait(false);
                _connection.DisposeAsync().ConfigureAwait(false);

            } catch {
                //
            }

            DrawMenu().ConfigureAwait(false);
        }

        /// <summary>
        /// Open the context menu on right click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NotifyIcon_Click(object? sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            Log.Debug("Notify icon right clicked?");
            var foo = e.Location;
            var target = _notifyIcon.ContextMenuStrip.PointToScreen(foo);
            target.X -= _notifyIcon.ContextMenuStrip.Height + 10;
            target.Y -= _notifyIcon.ContextMenuStrip.Width + 10;
            _notifyIcon.ContextMenuStrip.Show(target);
        }

        /// <summary>
        /// Opens the web manager on a double left click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NotifyIcon_DoubleClick(object? sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Left) {
                return;
            }

            Log.Debug("Double click");
            OpenManager_Click(sender, e);
        }

        /// <summary>
        /// build the context menu each time it opens to ensure appropriate options
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenuStrip_Opening(object? sender, CancelEventArgs e)
        {
            Log.Debug("Opening");
            if (_connection.State is not HubConnectionState.Connected or HubConnectionState.Connecting) {
                await Connect();
                if (_connection.State is HubConnectionState.Connected) await DrawMenu();
            }
            e.Cancel = false;
        }

        /// <summary>
        /// Show the settings dialogue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsCommand(object? sender, EventArgs e)
        {
            //Don't continue if teh service or settings are null
            if (_connection.State != HubConnectionState.Connected || _settings is null) return;

            _viewModel = new SettingsWindowViewModel(_connection, _states, _settings);
            _settingsWindow = new SettingsWindow(_viewModel, _connection);
            if (_settingsWindow.ShowDialog() == true)
            {
                try
                {
                    Log.Debug("Saving settings...");
                    SetSettings(_viewModel.WorkingSettings);
                    Log.Debug("Done.");
                }
                catch(Exception ex)
                {
                    Logger("Exception saving settings: " + ex.Message, LogEventLevel.Warning);
                    System.Windows.MessageBox.Show("Unable to save settings" + Environment.NewLine + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }     
                var oldPort = _settings.ServerPort;

                //The only setting that would require a restart of the service is the listening port.
                //If that gets changed notify the user to restart the service from the service snap in
                if (_viewModel.WorkingSettings.ServerPort != oldPort)
                {
                    System.Windows.MessageBox.Show("Server port changed! You will need to restart the service from the services snap in for the change to be applied", "Settings changed!", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            _settingsWindow = null;
            _viewModel = null;
        }

        // Automagically re-draw our context menu on state/settings changes, versus trying to do it when we 
        // open the context menu.
        private async Task DrawMenu() {
            await Task.FromResult(true);
            Log.Debug("Redrawing menu...");
            _notifyIcon.ContextMenuStrip.Items.Clear();

            if (_connection.State == HubConnectionState.Connected) 
            {
                    switch (_state)
                    {
                        case PlexState.Running:
                            _notifyIcon.ContextMenuStrip.Items.Add("Stop Plex", null, StopPlex_Click);
                            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                            _notifyIcon.ContextMenuStrip.Items.Add("Open Plex...", null, OpenManager_Click);
                            break;
                        case PlexState.Stopped:
                            _notifyIcon.ContextMenuStrip.Items.Add("Start Plex", null, StartPlex_Click);
                            break;
                        case PlexState.Pending:
                            _notifyIcon.ContextMenuStrip.Items.Add("Restart Pending");
                            break;
                        case PlexState.Updating:
                            _notifyIcon.ContextMenuStrip.Items.Add("Plex updating");
                            break;
                        case PlexState.Stopping:
                            _notifyIcon.ContextMenuStrip.Items.Add("Stopping");
                            break;
                        case PlexState.Unknown:
                        default:
                            _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");
                            break;
                    }
                if (!string.IsNullOrEmpty(GetDataDir())) _notifyIcon.ContextMenuStrip.Items.Add("PMS Data Folder", null, PMSData_Click);
                if (_settings != null) {
                    _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    var auxAppsToLink = _settings.AuxiliaryApplications.Where(aux => !string.IsNullOrEmpty(aux.Url))
                        .ToList();
                    if (auxAppsToLink.Count > 0) {
                        var auxAppsItem = new ToolStripMenuItem
                        {
                            Text = "Auxiliary Applications",
                            ForeColor = Color.FromArgb(232, 234, 237),
                            BackColor = Color.FromArgb(41, 42, 45)
                        };
                        auxAppsToLink.ForEach(aux => {
                            Log.Debug("Adding app: " + aux.Name);
                            var auxItem = new ToolStripMenuItem(aux.Name, null, (_, _) => {
                                try {
                                    var url = aux.Url;
                                    url = url.Replace("localhost", _connectionSettings.ServerAddress);
                                    url = url.Replace("127.0.0.1", _connectionSettings.ServerAddress);
                                    url = url.Replace("0.0.0.0", _connectionSettings.ServerAddress);
                                    Process.Start("explorer", url);
                                } catch (Exception ex) {
                                    Logger("Aux exception: " + ex.Message, LogEventLevel.Warning);
                                    System.Windows.Forms.MessageBox.Show(ex.Message, "Whoops!", MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                }
                            });
                            // If the aux app isn't running, don't enable click.
                            auxItem.Enabled = _states.ContainsKey(aux.Name) && _states[aux.Name];
                            auxItem.ForeColor = Color.FromArgb(232, 234, 237);
                            auxItem.BackColor = Color.FromArgb(41, 42, 45);
                            auxAppsItem.DropDownItems.Add(auxItem);
                        });
                        _notifyIcon.ContextMenuStrip.Items.Add(auxAppsItem);
                        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    }

                    var settingsItem = _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, SettingsCommand);
                    if (_settingsWindow != null) {
                        settingsItem.Enabled = false;
                    }
                    _notifyIcon.ContextMenuStrip.Refresh();
                }
            }
            else
            {
                _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");
                _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                _notifyIcon.ContextMenuStrip.Items.Add("PMS Data", null, PMSData_Click);
            }
            var connectionSettingsItem = _notifyIcon.ContextMenuStrip.Items.Add("Connection Settings", null, ConnectionSettingsCommand);
            if (_connectionSettingsWindow != null)
                connectionSettingsItem.Enabled = false;

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            
            _notifyIcon.ContextMenuStrip.Items.Add("View Log", null, ViewLogs_Click);
            var aboutItem = _notifyIcon.ContextMenuStrip.Items.Add("About", null, AboutCommand);
            if (AboutWindow.Shown)
                aboutItem.Enabled = false;
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, ExitCommand);
        }

        private void SetSettings(Settings viewModelWorkingSettings) {
            try {
                _connection.SendAsync("setSettings", viewModelWorkingSettings);
            } catch (Exception e) {
                Logger("Exception saving settings: " + e.Message);
            }
        }

        private void Logger(string message, LogEventLevel level = LogEventLevel.Debug) 
        {
            Log.Write(level, message);
            if (_connection.State == HubConnectionState.Connected) {
                _connection.SendAsync("logMessage", message, level);
            }
        }
        private string GetTheme() {
            return _settings is null ? "Dark.Amber" : _settings.Theme;
        }
        
        /// <summary>
        /// Show the connection settings dialogue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectionSettingsCommand(object? sender, EventArgs e) 
        {
            var theme = GetTheme();
            _connectionSettingsWindow = new ConnectionSettingsWindow(theme);
            if (_connectionSettingsWindow.ShowDialog() == true)
            {
                //if the user saved the settings, then reconnect using the new values
                try
                {
                    Disconnect();
                    Connect().ConfigureAwait(false);
                } 
                catch (Exception ex)
                {
                    Logger("Exception on connection setting command" + ex.Message, LogEventLevel.Warning);
                }
            }
            _connectionSettingsWindow = null;
        }

        private string PlexStateString() {
            switch (_state) {
                case PlexState.Running:
                    return $"Plex {_state.ToString()}";
                case PlexState.Stopped:
                    return $"Plex {_state.ToString()}";
                case PlexState.Updating:
                    return $"Plex is {_state.ToString()}";
                case PlexState.Pending:
                    return $"Plex Start {_state.ToString()}";
                case PlexState.Stopping:
                    return $"Plex {_state.ToString()}";
                case PlexState.Unknown:
                default:
                    return "Unknown state.";
            }
        }

        /// <summary>
        /// Open the About dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutCommand(object? sender, EventArgs e)
        {
            AboutWindow.ShowAboutDialog(GetTheme());
        }

        /// <summary>
        /// Close the notify icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitCommand(object? sender, EventArgs e)
        {
            Disconnect();
            ExitThread();
        }

        /// <summary>
        /// Start Plex
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartPlex_Click(object? sender, EventArgs e) 
        {
            try
            {
                _connection.SendAsync("startPlex");
            }
            catch (Exception ex) 
            {
                Logger("Exception on startPlex click: " + ex, LogEventLevel.Warning);
                //Disconnect();
            }
        }

        /// <summary>
        /// Stop Plex
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopPlex_Click(object? sender, EventArgs e) 
        {
            //stop it
            try {
                _connection.SendAsync("stopPlex");
            }
            catch (Exception ex)
            {
                Logger("Exception stopping Plex..." + ex.Message);
                //Disconnect();
            }
        }

        /// <summary>
        /// Try to open the web manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenManager_Click(object? sender, EventArgs e)
        {
            //this is pretty old school, we should probably go to app.plex.tv...
            //The web manager should be located at the server address in the connection settings
            Process.Start("explorer","http://" + _connectionSettings.ServerAddress + ":32400/web");
        }

        /// <summary>
        /// View the server log file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewLogs_Click(object? sender, EventArgs e) 
        {
            Log.Debug("View log click...");
            var sa = _connectionSettings.ServerAddress;
            // Use windows shell to open log file in whatever app the user uses...
            var fileToOpen = string.Empty;
            try
            {
                // If we're local to the service, just open the file.
                if (sa is "127.0.0.1" or "0.0.0.0" or "localhost") {
                    fileToOpen = _logPath ?? PlexDirHelper.LogFile;
                    Log.Debug("Using local file: " + fileToOpen);
                } else {
                    Logger("Requesting log.");
                    // Otherwise, request the log data from the server, save it to a temp file, and open that.
                    var logData = GetLog();
                    if (logData == null) {
                        Logger("No log data received.", LogEventLevel.Warning);
                        return;
                    }
                    Logger("Data received: " + logData);
                    var tmpPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var tmpFile = Path.Combine(tmpPath, "pms_s.log");
                    Logger("Writing to " + tmpFile);
                    File.WriteAllText(tmpFile, logData);
                    if (File.Exists(tmpFile)) fileToOpen = tmpFile;
                }

                if (string.IsNullOrEmpty(fileToOpen)) return;
                Log.Debug("Opening: " + fileToOpen);
                Process.Start("explorer", fileToOpen);
            }
            catch (Exception ex) 
            {
                Logger("Exception viewing logs: " + ex.Message);
                //Disconnect();
            }
        }

        private string GetLog() {
            try {
                return _connection.InvokeAsync<string>("getLog").Result;
            } catch (Exception) {
                return string.Empty;
            }
        }

        private void PMSData_Click(object? sender, EventArgs e) 
        {
            //Open a windows explorer window to PMS data
            var dir = GetDataDir();
            try 
            {                
                if (!string.IsNullOrEmpty(dir)) Process.Start("explorer", $@"{dir}");
            }
            catch (Exception ex)
            {
                Logger($"Error opening PMS Data folder at {dir}: " + ex.Message, LogEventLevel.Warning);
                //Disconnect();
            }
        }

        private string GetDataDir() 
        {
            var dir = string.Empty;
            var path = _pmsPath ?? string.Empty;
            if (string.IsNullOrEmpty(path)) return dir;
            // If we're not local, see if we can access PMS data dir over UNC
            if (!_connectionSettings.IsLocal) 
            {
                var drive = path.Substring(0, 1);
                var ext = path.Substring(3);
                var unc = Path.Combine("\\\\" + _connectionSettings.ServerAddress, drive + "$", ext);
                if (Directory.Exists(unc)) dir = unc;
            } 
            else 
            {
                dir = path;
            }

            return dir;
        }
    }
}

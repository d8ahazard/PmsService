using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.SignalR.Client;
using PlexServiceCommon;
using Serilog;

namespace PlexServiceTray
{
    public class SettingsWindowViewModel:ObservableObject {
        private readonly HubConnection _connection;
        /// <summary>
        /// The server endpoint port
        /// </summary>
        public int ServerPort
        {
            get => WorkingSettings.ServerPort;
            set 
            {
                if (WorkingSettings.ServerPort == value) return;

                WorkingSettings.ServerPort = value;
                OnPropertyChanged(nameof(ServerPort));
            }
        }

        /// <summary>
        /// Plex restart delay
        /// </summary>
        public int RestartDelay
        {
            get => WorkingSettings.RestartDelay;
            set 
            {
                if (WorkingSettings.RestartDelay == value) return;

                WorkingSettings.RestartDelay = value;
                OnPropertyChanged(nameof(RestartDelay));
            }
        }

        public bool AutoRestart
        {
            get => WorkingSettings.AutoRestart;
            set 
            {
                if (WorkingSettings.AutoRestart == value) return;

                WorkingSettings.AutoRestart = value;
                OnPropertyChanged(nameof(AutoRestart));
            }
        }
        
        public bool AutoRemount
        {
            get => WorkingSettings.AutoRemount;
            set 
            {
                if (WorkingSettings.AutoRemount == value) return;

                WorkingSettings.AutoRemount = value;
                OnPropertyChanged(nameof(AutoRemount));
            }
        }
        
        public int AutoRemountCount
        {
            get => WorkingSettings.AutoRemountCount;
            set 
            {
                if (WorkingSettings.AutoRemountCount == value) return;

                WorkingSettings.AutoRemountCount = value;
                OnPropertyChanged(nameof(AutoRemountCount));
            }
        }
        
        public int AutoRemountDelay
        {
            get => WorkingSettings.AutoRemountDelay;
            set 
            {
                if (WorkingSettings.AutoRemountDelay == value) return;

                WorkingSettings.AutoRemountDelay = value;
                OnPropertyChanged(nameof(AutoRemountDelay));
            }
        }
        
        public string Theme
        {
            get => WorkingSettings.Theme?.Replace("."," ") ?? "Dark Red";
            set 
            {
                if (WorkingSettings.Theme?.Replace(" ", ".") == value) return;
                WorkingSettings.Theme = value.Replace(" ", ".");
                OnPropertyChanged(nameof(Theme));
            }
        }

        public bool StartPlexOnMountFail
        {
            get => WorkingSettings.StartPlexOnMountFail;
            set 
            {
                if (WorkingSettings.StartPlexOnMountFail == value) return;

                WorkingSettings.StartPlexOnMountFail = value;
                OnPropertyChanged(nameof(StartPlexOnMountFail));
            }
        }
        
        private int _selectedTab;

        public int SelectedTab
        {
            get => _selectedTab;
            set 
            {
                if (_selectedTab == value) return;

                _selectedTab = value;
                OnPropertyChanged(nameof(SelectedTab));
                OnPropertyChanged(nameof(RemoveToolTip));
                OnPropertyChanged(nameof(AddToolTip));
            }
        }


        private readonly ObservableCollection<AuxiliaryApplicationViewModel> _auxiliaryApplications = new();
        /// <summary>
        /// Collection of Auxiliary applications to run alongside plex
        /// </summary>
        public ObservableCollection<AuxiliaryApplicationViewModel> AuxiliaryApplications
        {
            get => _auxiliaryApplications;
            init {
                if (_auxiliaryApplications == value) {
                    return;
                }

                _auxiliaryApplications = value;
                OnPropertyChanged(nameof(AuxiliaryApplications));
            }
        }

        private AuxiliaryApplicationViewModel? _selectedAuxApplication;

        public AuxiliaryApplicationViewModel? SelectedAuxApplication
        {
            get => _selectedAuxApplication;
            set
            {
                if (_selectedAuxApplication != value)
                {
                    _selectedAuxApplication = value;
                    OnPropertyChanged(nameof(SelectedAuxApplication));
                    OnPropertyChanged(nameof(RemoveToolTip));
                }
            }
        }

        private readonly ObservableCollection<DriveMapViewModel> _driveMaps = new();

        public ObservableCollection<DriveMapViewModel> DriveMaps
        {
            get => _driveMaps;
            init {
                if (_driveMaps == value) {
                    return;
                }

                _driveMaps = value;
                OnPropertyChanged(nameof(DriveMaps));
            }
        }

        private DriveMapViewModel? _selectedDriveMap;

        public DriveMapViewModel? SelectedDriveMap
        {
            get => _selectedDriveMap;
            set {
                if (_selectedDriveMap == value) {
                    return;
                }

                _selectedDriveMap = value;
                OnPropertyChanged(nameof(SelectedDriveMap));
                OnPropertyChanged(nameof(RemoveToolTip));
            }
        }

        public string RemoveToolTip
        {
            get
            {
                switch (SelectedTab)
                {
                    case 0:
                        if (SelectedAuxApplication != null)
                        {
                            return "Remove " + SelectedAuxApplication.Name;
                        }
                        break;
                    case 1:
                        if (SelectedDriveMap != null)
                        {
                            return "Remove Drive Map " + SelectedDriveMap.DriveLetter + " -> " + SelectedDriveMap.ShareName;
                        }
                        break;
                }
                return "Nothing selected!";
            }
        }

        public string? AddToolTip
        {
            get {
                return SelectedTab switch {
                    0 => "Add Auxiliary Application",
                    1 => "Add Drive Map",
                    _ => null
                };
            }
        }

        private bool? _dialogResult;

        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                if (_dialogResult != value)
                {
                    _dialogResult = value;
                    OnPropertyChanged(nameof(DialogResult));
                }
            }
        }

        /// <summary>
        /// Use one settings instance for the life of the window.
        /// </summary>
        public Settings WorkingSettings { get; set; }

        public SettingsWindowViewModel(HubConnection connection, Dictionary<string, bool> states,
            Settings settings) {
            _connection = connection;
            _states = states;
            WorkingSettings = settings;
            AuxiliaryApplications = new ObservableCollection<AuxiliaryApplicationViewModel>();
            DriveMaps = new ObservableCollection<DriveMapViewModel>();

            WorkingSettings.AuxiliaryApplications.ForEach(x =>
            {
                var auxApp = new AuxiliaryApplicationViewModel(x, connection, states, this);
                AuxiliaryApplications.Add(auxApp);
            });

            WorkingSettings.DriveMaps.ForEach(x => DriveMaps.Add(new DriveMapViewModel(x)));

            if (AuxiliaryApplications.Count > 0)
            {
                AuxiliaryApplications[0].IsExpanded = true;
            }
        }

        /// <summary>
        /// Allow the user to add a new Auxiliary application
        /// </summary>
        #region AddCommand
        RelayCommand _addCommand;
        public RelayCommand AddCommand => _addCommand ??= new RelayCommand(OnAdd);

        private void OnAdd(object parameter)
        {
            switch (SelectedTab)
            {
                case 0:
                    var newAuxApp = new AuxiliaryApplication {
                        Name = "New Auxiliary Application"
                    };
                    var newAuxAppViewModel = new AuxiliaryApplicationViewModel(newAuxApp, _connection, _states, this)
                        {
                            IsExpanded = true
                        };
					AuxiliaryApplications.Add(newAuxAppViewModel);
                    break;
                case 1:
                    var newDriveMap = new DriveMap(@"\\computer\share", "Z");
                    var newDriveMapViewModel = new DriveMapViewModel(newDriveMap);
                    DriveMaps.Add(newDriveMapViewModel);
                    break;
            }
            
        }

        #endregion AddCommand

        /// <summary>
        /// Remove the selected auxiliary application
        /// </summary>
        #region RemoveCommand
        RelayCommand _removeCommand;
        public RelayCommand RemoveCommand => _removeCommand ??= new RelayCommand(OnRemove, CanRemove); 

        private bool CanRemove(object parameter)
        {
            return SelectedTab switch
            {
                0 => SelectedAuxApplication != null,
                1 => SelectedDriveMap != null,
                _ => false,
            };
        }

        private void OnRemove(object parameter)
        {
            switch (SelectedTab)
            {
                case 0:
                    if (SelectedAuxApplication == null) return;
					AuxiliaryApplications.Remove(SelectedAuxApplication);
                    break;
                case 1:
                    if (SelectedDriveMap == null) return;
                    DriveMaps.Remove(SelectedDriveMap);
                    break;
            }
            
        }

        #endregion RemoveCommand

        /// <summary>
        /// Save the settings file
        /// </summary>
        #region SaveCommand
        RelayCommand _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(OnSave, CanSave);

        private bool CanSave(object parameter)
        {
            return ServerPort > 0 && string.IsNullOrEmpty(Error) && !AuxiliaryApplications.Any(a => !string.IsNullOrEmpty(a.Error) || string.IsNullOrEmpty(a.Name)) && !DriveMaps.Any(dm => !string.IsNullOrEmpty(dm.Error) || string.IsNullOrEmpty(dm.ShareName) || string.IsNullOrEmpty(dm.DriveLetter));
        }

        private void OnSave(object parameter)
        {
            WorkingSettings.AuxiliaryApplications.Clear();
            foreach (var aux in AuxiliaryApplications)
            {
                WorkingSettings.AuxiliaryApplications.Add(aux.GetAuxiliaryApplication());
            }
            WorkingSettings.DriveMaps.Clear();
            foreach(var dMap in DriveMaps)
            {
                WorkingSettings.DriveMaps.Add(dMap.GetDriveMap());
            }
            DialogResult = true;
        }

        #endregion SaveCommand

        /// <summary>
        /// Close the dialogue without saving changes
        /// </summary>
        #region CancelCommand
        RelayCommand _cancelCommand;

        private readonly Dictionary<string, bool> _states;
        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(OnCancel);

        private void OnCancel(object parameter)
        {
            Log.Debug("ON CANCEL");
            DialogResult = false;
        }

        #endregion CancelCommand

        
    }
}

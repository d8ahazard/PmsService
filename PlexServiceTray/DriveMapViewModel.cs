using System;
using PlexServiceCommon;
using System.ComponentModel.DataAnnotations;
using Meziantou.Framework.Win32;
using Serilog;

namespace PlexServiceTray
{
    public class DriveMapViewModel : ObservableObject {
        private string _user;
        private string _pass;
        
        [Required(ErrorMessage = "Please enter a UNC path to map")]
        [RegularExpression(@"^\\\\[a-zA-Z0-9\.\-_]{1,}(\\[a-zA-Z0-9\.\x20\-_]{1,}[\$]{0,1}){1,}$", ErrorMessage = "Please enter a UNC path to map")]
        public string ShareName
        {
            get => _driveMap.ShareName;
            set 
            {
                if (_driveMap.ShareName == value) return;

                _driveMap.ShareName = value;
                OnPropertyChanged(nameof(ShareName));
            }
        }

        [Required(ErrorMessage = "Please enter a single character A-Z")]
        [RegularExpression("[a-zA-Z]", ErrorMessage = "Please enter a single character A-Z")]
        public string DriveLetter
        {
            get => _driveMap.DriveLetter;
            set 
            {
                if (_driveMap.DriveLetter == value) return;

                _driveMap.DriveLetter = value;
                OnPropertyChanged(nameof(DriveLetter));
            }
        }

        public string DriveUsername {
            get {
                if (string.IsNullOrEmpty(_user)) {
                    _user = GetUserName();
                }
                return _user;
            }
            set
            {
                if (_user == value) return;
                _user = value;
                OnPropertyChanged(nameof(DriveUsername));
                SaveCredentials();
            }
        }

        public string DrivePassword {
            get {
                if (string.IsNullOrEmpty(_pass)) {
                    _pass = GetPassword();
                }
                return _pass;
            }
            set
            {
                if (_pass == value) return;
                _pass = value;
                OnPropertyChanged(nameof(DrivePassword));
                SaveCredentials();
            }
        }
        
        private string GetUserName()
        {
            
            // Get a credential from the credential manager
            var cred = CredentialManager.ReadCredential($"PMSS_{DriveLetter}");
            return cred?.UserName ?? string.Empty;
        }
        
        private string GetPassword()
        {
            
            // Get a credential from the credential manager
            var cred = CredentialManager.ReadCredential($"PMSS_{DriveLetter}");
            return cred?.Password ?? string.Empty;
        }
        
        private void SaveCredentials()
        {
            if (_user == string.Empty || _pass == string.Empty) return;
            // Save the credentials to the credential manager
            try {
                CredentialManager.WriteCredential($"PMSS_{DriveLetter}", _user, _pass,
                    CredentialPersistence.Enterprise);
                _driveMap.LoadCredentials();
            } catch (Exception ex) {
                Log.Debug("Exception saving credentials: " + ex.Message);
            }
        }

        private readonly DriveMap _driveMap;
        public DriveMapViewModel(DriveMap driveMap) {
            _driveMap = driveMap;
            _user = GetUserName();
            _pass = GetPassword();
            ValidationContext = this;
        }

        public DriveMap GetDriveMap()
        {
            return _driveMap;
        }
    }
}

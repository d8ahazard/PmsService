using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Meziantou.Framework.Win32;
using Serilog;

namespace PlexServiceCommon; 

[JsonObject(MemberSerialization=MemberSerialization.OptIn)]
public class DriveMap
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)] private static extern int WNetAddConnection2A(ref NetworkResource netRes, string password, string username, int flags);
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)] private static extern int WNetCancelConnection2A(string name, int flags, int force);

    [StructLayout(LayoutKind.Sequential)]
    private struct NetworkResource
    {
        public int Scope;
        public int Type;
        public int DisplayType;
        public int Usage;
        public string LocalName;
        public string RemoteName;
        private readonly string Comment;
        private readonly string Provider;
    }

    [JsonProperty]
    public string ShareName { get; set; }

    [JsonProperty]
    public string DriveLetter { get; set; }

    private string _driveUsername;

    private string _drivePassword;

    [JsonConstructor]
    private DriveMap()
    {
        ShareName = string.Empty;
        DriveLetter = string.Empty;
        LoadCredentials();
    }

    public DriveMap(string shareName, string driveLetter)
    {
        ShareName = shareName;
        DriveLetter = driveLetter;
        LoadCredentials();
    }

    /// <summary>
    /// Map network drive
    /// </summary>
    /// <param name="force">do unmap first</param>
    public async Task MapDrive(bool force) {
        await Task.FromResult(true);
        if (DriveLetter.Length > 0)
        {
            var drive = string.Concat(DriveLetter.AsSpan(0,1), ":");
            try {
                var files = Directory.GetDirectories(drive);
                if (files.Length > 0)
                {
                    Log.Information($"Drive {drive} is already mapped");
                    return;
                }
            } catch (Exception e) {
                Log.Debug($"Exception checking drive map {drive}: {e.Message}");
            }
            //create struct data
            var netRes = new NetworkResource {
                Scope = 2,
                Type = 0x1,
                DisplayType = 3,
                Usage = 1,
                RemoteName = ShareName,
                LocalName = drive
            };
            //if force, unmap ready for new connection
            if (force)
            {
                try
                {
                    UnMapDrive(true);
                } catch (Exception e){
                    Log.Warning("Exception unmapping drive: " + e.Message);
                }
            }

            string user = null;
            string pass = null;
            //call and return
            if (!string.IsNullOrEmpty(_drivePassword) && !string.IsNullOrEmpty(_driveUsername)) {
                user = _driveUsername;
                pass = _drivePassword;
            }
            var i = WNetAddConnection2A(ref netRes, pass, user, 0);

            if (i > 0)
                throw new System.ComponentModel.Win32Exception(i);
                
        }
        else
        {
            throw new Exception("Invalid drive letter: " + DriveLetter);
        }
    }

    public void LoadCredentials() {
        try {
            var cred = CredentialManager.ReadCredential($"PMSS_{DriveLetter}");
            if (cred == null) {
                return;
            }

            _driveUsername = cred.UserName;
            _drivePassword = cred.Password;
        } catch (Exception e) {
            Log.Warning("Exception loading credentials: " + e.Message);
        }
    }

    /// <summary>
    /// Unmap network drive
    /// </summary>
    /// <param name="force">Specifies whether the disconnection should occur if there are open files or jobs on the connection. If this parameter is FALSE, the function fails if there are open files or jobs.</param>
    public void UnMapDrive(bool force)
    {
        if (DriveLetter.Length > 0)
        {
            var drive = DriveLetter[..1] + ":";

            try {
                //call unmap and return
                var i = WNetCancelConnection2A(drive, 0, Convert.ToInt32(force));

                if (i > 0)
                    Log.Warning("Exception unmapping drive: " + i);    
            } catch (Exception e) {
                Log.Warning("Exception unmapping drive: " + e.Message);
            }
            
        }
        else
        {
            throw new Exception("Invalid drive letter: " + DriveLetter);
        }
    }
}
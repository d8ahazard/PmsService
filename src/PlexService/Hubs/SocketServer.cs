#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PlexService.Models;
using PlexServiceCommon;
using Serilog;
using Serilog.Events;

#endregion

namespace PlexService.Hubs {
	public class SocketServer : Hub {
		private readonly PlexMediaServerService? _ps;
		

		public SocketServer() {
			_ps = PlexMediaServerService.Instance;
		}

		public void SetSettings(Settings settings) {
			try {
				Log.Information("Saving settings.");
				SettingsHandler.Save(settings);
				Clients.All.SendAsync("settings", settings);
			} catch (Exception e) {
				Log.Warning("Exception caught on settings save: " + e.Message + " at " + e.StackTrace);
			}
		}

		public Task StopPlex() {
			_ps?.Monitor.Stop();
			return Task.CompletedTask;
		}
		
		public Task StartPlex() {
			_ps?.Monitor.Start();
			return Task.CompletedTask;
		}
		
		public void StartAuxApp(string name) {
			if (_ps == null) return;
			Clients.All.SendAsync("auxState", new Tuple<string, bool>(name, _ps.Monitor.StartAuxApp(name)));
		}
		
		public void StopAuxApp(string name) {
			if (_ps == null) return;
			Clients.All.SendAsync("auxState", new Tuple<string, bool>(name, !_ps.Monitor.StopAuxApp(name)));
		}

		public Task LogMessage(string message, LogEventLevel level) {
			Log.Write(level, message);
			return Task.CompletedTask;
		}
		
		public override async Task OnConnectedAsync() {
			try {
				Log.Information("Client Connected: " + Context.ConnectionId);
				if (_ps == null) return;
				// #Todo: Create one single object that holds all this stuff so we can 
				// simplify sending data to clients?
				await Clients.Caller.SendAsync("settings", SettingsHandler.Load());
				await Clients.Caller.SendAsync("state", _ps.State);
				await Clients.Caller.SendAsync("pmsPath", PlexDirHelper.PmsDataPath);
				await Clients.Caller.SendAsync("logPath", PlexDirHelper.LogFile);
				var states = new Dictionary<string, bool>();
				foreach (var aa in _ps.Monitor.AuxAppMonitors) {
					states[aa.Name] = aa.Running;
				}

				if (states.Count > 0) await Clients.Caller.SendAsync("states", states);
			} catch (Exception e) {
				Log.Warning("Connect exception: " + e.Message);
			}

			await base.OnConnectedAsync();
		}

		public override Task OnDisconnectedAsync(Exception? exception) {
			Log.Information("Client Disconnected: " + Context.ConnectionId);
			return base.OnDisconnectedAsync(exception);
		}
	}
}
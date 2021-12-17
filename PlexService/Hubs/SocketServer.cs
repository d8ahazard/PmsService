#region

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PlexService.Models;
using PlexServiceCommon;
using Serilog;
using Serilog.Events;

#endregion

namespace PlexService.Hubs {
	public class SocketServer : Hub {
		private readonly PlexMediaServerService _ps;
		

		public SocketServer(PlexMediaServerService ps) {
			_ps = ps;
		}


		public Task SetSettings(Settings settings) {
			try {
				Log.Information("Saving settings.");
				SettingsHandler.Save(settings);
				Clients.All.SendAsync("settings", settings);
			} catch (Exception e) {
				Log.Warning("Exception caught on settings save: " + e.Message + " at " + e.StackTrace);
			}

			return Task.CompletedTask;
		}

		public async Task<Settings?> GetSettings() {
			Log.Debug("Settings requested...");
			try {
				await Task.FromResult(true);
				var res = SettingsHandler.Load();
				Log.Debug("Returning...");
				return res;
			} catch (Exception e) {
				Log.Warning("Exception getting/sending settings: " + e.Message);
			}

			return null;
		}

		public PlexState GetState() {
			Log.Debug("State requested...");
			try {
				return _ps.State;
			} catch (Exception e) {
				Log.Warning("Exceptions sending state: " + e.Message);
			}

			return PlexState.Stopped;
		}

		public Task StopPlex() {
			_ps.Monitor.EndPlex();
			return Task.CompletedTask;
		}
		
		public Task StartPlex() {
			_ps.Monitor.Start();
			return Task.CompletedTask;
		}
		
		public Task StartAuxApp(string name) {
			_ps.Monitor.StartAuxApp(name);
			return Task.CompletedTask;
		}
		
		public Task StopAuxApp(string name) {
			_ps.Monitor.StopAuxApp(name);
			return Task.CompletedTask;
		}
		

		public Task LogMessage(string message, LogEventLevel level) {
			Log.Write(level, message);
			return Task.CompletedTask;
		}

		public bool IsAuxAppRunning(string name) {
			return _ps.Monitor.IsAuxAppRunning(name);
		}

		public override async Task OnConnectedAsync() {
			try {
				Log.Information("Client Connected: " + Context.ConnectionId);
				await Clients.Caller.SendAsync("settings", SettingsHandler.Load());
				await Clients.Caller.SendAsync("state", _ps.State);
				await Clients.Caller.SendAsync("pmsPath", PlexDirHelper.PmsDataPath);
				await Clients.Caller.SendAsync("logPath", PlexDirHelper.LogFile);
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
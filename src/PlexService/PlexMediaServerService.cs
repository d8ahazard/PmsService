using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using PlexService.Hubs;
using PlexService.Models;
using PlexServiceCommon;
using Serilog;

namespace PlexService
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public class PlexMediaServerService : BackgroundService {
        public static PlexMediaServerService Instance;
        public readonly PmsMonitor Monitor;
        private Task _serviceTask;
        
        public PlexState State => Monitor.State;

        public PlexMediaServerService(IHubContext<SocketServer> hubContext) {
            // HubContext is what we use to talk to any listening websocket clients
            Log.Debug("Instantiating service..");
            Monitor = new PmsMonitor(hubContext);
            Instance = this;
            Log.Debug("PMSService Created.");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) {
            _serviceTask = Task.Run(async () => {
                Log.Debug("Executing service");
                while (!stoppingToken.IsCancellationRequested) {
                    await Task.Delay(50, CancellationToken.None);
                }
                Log.Debug("Stopping monitor.");
                Monitor.Stop();
                Log.Debug("Stopped.");
            });
            return Task.CompletedTask;
        }
    }
}

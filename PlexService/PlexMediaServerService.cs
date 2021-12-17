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
    public partial class PlexMediaServerService : BackgroundService
    {
        public PmsMonitor Monitor;
        private static PlexMediaServerService _instance;

        public PlexState State => Monitor.State;

        public PlexMediaServerService(IHubContext<SocketServer> hubContext) {
            // HubContext is what we use to talk to any listening websocket clients
            Monitor = new PmsMonitor(hubContext);
            _instance = this;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) {
            return Task.Run(async () => {
                Monitor.Start();
                while (!stoppingToken.IsCancellationRequested) {
                    await Task.Delay(50, CancellationToken.None);
                }
                Log.Debug("Stopping...");
                Monitor.Stop();
                Log.Debug("Stopped.");
            }, CancellationToken.None);
        }
        
        public static PlexMediaServerService GetInstance() {
            return _instance;
        }
    }
}

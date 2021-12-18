﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using PlexService.Hubs;
using PlexService.Models;
using PlexServiceCommon;

namespace PlexService
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public class PlexMediaServerService : BackgroundService {
        public static PlexMediaServerService? Instance;
        public readonly PmsMonitor Monitor;
        
        public PlexState State => Monitor.State;

        public PlexMediaServerService(IHubContext<SocketServer> hubContext) {
            // HubContext is what we use to talk to any listening websocket clients
            Monitor = new PmsMonitor(hubContext);
            Instance = this;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(50, CancellationToken.None);
            }
            Monitor.Stop();
        }

        public static PlexMediaServerService? GetInstance() {
            return Instance;
        }
        
    }
}

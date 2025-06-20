// FINAL FIX
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ZooTrack.Hubs
{
    [Authorize] // Let the middleware handle authorization for the whole hub.
    public class CameraHub : Hub
    {
        private readonly ILogger<CameraHub> _logger;

        public CameraHub(ILogger<CameraHub> logger)
        {
            _logger = logger;
        }

        public async Task SubscribeToCamera(int cameraId)
        {
            var groupName = $"camera-{cameraId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Authenticated client {ConnectionId} subscribed to {GroupName}", Context.ConnectionId, groupName);
        }

        public async Task UnsubscribeFromCamera(int cameraId)
        {
            var groupName = $"camera-{cameraId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} unsubscribed from {GroupName}", Context.ConnectionId, groupName);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogWarning(exception, "Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}

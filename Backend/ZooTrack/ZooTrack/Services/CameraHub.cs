// ZooTrack.WebAPI/Hubs/CameraHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ZooTrack.Hubs
{
    public class CameraHub : Hub
    {
        // Method for clients to potentially call (optional for now)
        // public async Task SendCommandToServer(string command)
        // {
        //     // Process command if needed
        //     await Clients.All.SendAsync("ReceiveLog", $"Command received: {command}");
        // }

        // Server will call this method on clients
        // Clients will register a handler for "ReceiveFrame"
        public async Task SendFrameToClients(byte[] frameData)
        {
            if (Clients != null)
            { // Ensure Clients is not null
                await Clients.All.SendAsync("ReceiveFrame", frameData);
            }
        }

        public async Task SendStatusUpdate(string status)
        {
            if (Clients != null)
            {
                await Clients.All.SendAsync("ReceiveStatus", status);
            }
        }

        public override async Task OnConnectedAsync()
        {
            // Optional: Log connection
            Context.GetHttpContext()?.RequestServices.GetService<ILogger<CameraHub>>()?.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
            // Maybe send initial status?
            // await SendStatusUpdate("Connected to hub.");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Optional: Log disconnection
            Context.GetHttpContext()?.RequestServices.GetService<ILogger<CameraHub>>()?.LogWarning(exception, "Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
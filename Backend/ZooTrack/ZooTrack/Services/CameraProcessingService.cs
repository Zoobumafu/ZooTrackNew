using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZooTrack.Hubs;

namespace ZooTrack.Services
{
    public class CameraProcessingService : BackgroundService
    {
        private readonly ILogger<CameraProcessingService> _logger;
        private readonly CameraService _cameraService;
        private readonly IHubContext<CameraHub> _hubContext;

        public CameraProcessingService(
            ILogger<CameraProcessingService> logger,
            CameraService cameraService,
            IHubContext<CameraHub> hubContext)
        {
            _logger = logger;
            _cameraService = cameraService;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraProcessingService starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var activeCameraIds = _cameraService.GetActiveCameraIds();

                    if (!activeCameraIds.Any())
                    {
                        // No active cameras, so wait before checking again
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    foreach (var cameraId in activeCameraIds)
                    {
                        var frameData = _cameraService.ProcessFrame(cameraId);

                        if (frameData != null)
                        {
                            var groupName = $"camera-{frameData.CameraId}";
                            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveFrame", frameData.JpegBytes, stoppingToken);

                            string status = frameData.TargetDetected
                                ? $"Processing... Target detected: {string.Join(", ", frameData.DetectedTargets)}"
                                : "Processing... Monitoring...";

                            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveStatus", status, stoppingToken);
                        }
                    }

                    // Control overall frame rate to prevent high CPU usage
                    await Task.Delay(50, stoppingToken); // ~20 FPS loop for all cameras combined
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("CameraProcessingService execution was cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in CameraProcessingService execution loop.");
                    // Wait a bit before retrying to avoid fast failure loops
                    await Task.Delay(2000, stoppingToken);
                }
            }

            _logger.LogInformation("CameraProcessingService execution completed.");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("CameraProcessingService is stopping.");
            _cameraService.Dispose(); // Dispose all camera instances
            return base.StopAsync(cancellationToken);
        }
    }
}
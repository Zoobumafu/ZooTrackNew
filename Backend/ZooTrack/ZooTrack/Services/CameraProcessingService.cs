// ZooTrack.WebAPI/Services/CameraProcessingService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZooTrack.Hubs; // Adjust namespace if needed

namespace ZooTrack.Services
{
    public class CameraProcessingService : BackgroundService
    {
        private readonly ILogger<CameraProcessingService> _logger;
        private readonly CameraService _cameraService;
        private readonly IHubContext<CameraHub> _hubContext;

        public CameraProcessingService(
            ILogger<CameraProcessingService> logger,
            CameraService cameraService, // Inject the singleton service
            IHubContext<CameraHub> hubContext)
        {
            _logger = logger;
            _cameraService = cameraService;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraProcessingService starting.");

            stoppingToken.Register(() =>
                _logger.LogInformation("CameraProcessingService stopping token invoked."));

            // Wait until processing is explicitly started via API or other means
            while (!stoppingToken.IsCancellationRequested && !_cameraService.IsProcessing)
            {
                //_logger.LogDebug("CameraProcessingService waiting for start signal...");
                await Task.Delay(1000, stoppingToken); // Check every second
                                                       // Send status update to clients?
                await SendStatusAsync("Idle. Waiting for start command.");
            }

            _logger.LogInformation("CameraProcessingService received start signal. Beginning processing loop.");
            await SendStatusAsync("Processing starting...");


            if (!_cameraService.IsInitialized)
            {
                _logger.LogWarning("Attempting lazy initialization of CameraService...");
                if (!_cameraService.InitializeCameraAndYolo())
                {
                    _logger.LogError("Initialization failed in background service. Stopping.");
                    await SendStatusAsync("Error: Failed to initialize camera or YOLO model.");
                    return; // Stop the service if init fails
                }
            }

            // Main processing loop
            while (!stoppingToken.IsCancellationRequested && _cameraService.IsProcessing)
            {
                if (!_cameraService.IsInitialized)
                {
                    _logger.LogWarning("Processing loop running but camera service not initialized. Attempting re-init...");
                    await SendStatusAsync("Error: Service not initialized. Retrying...");
                    if (!_cameraService.InitializeCameraAndYolo())
                    {
                        _logger.LogError("Re-initialization failed. Stopping processing.");
                        await SendStatusAsync("Error: Initialization failed.");
                        _cameraService.StopProcessing(); // Ensure the flag is false
                        continue; // Skip to next loop iteration check
                    }
                    else
                    {
                        await SendStatusAsync("Initialization successful. Resuming processing.");
                    }
                }

                try
                {
                    var frameData = _cameraService.ProcessFrame();

                    if (frameData != null && frameData.JpegBytes.Length > 0)
                    {
                        // Send frame to all connected SignalR clients
                        await _hubContext.Clients.All.SendAsync("ReceiveFrame", frameData.JpegBytes, stoppingToken);

                        // Optionally send detection info
                        if (frameData.TargetDetected)
                        {
                            // Could send a specific message for detections if needed
                            // await _hubContext.Clients.All.SendAsync("TargetDetected", frameData.DetectedTargets, stoppingToken);
                            await SendStatusAsync($"Processing... Target detected: {string.Join(", ", frameData.DetectedTargets)}");

                        }
                        else
                        {
                            await SendStatusAsync("Processing... Monitoring...");
                        }
                    }
                    else if (frameData == null)
                    {
                        // Error occurred during frame processing (logged in CameraService)
                        _logger.LogWarning("ProcessFrame returned null. Check CameraService logs.");
                        await SendStatusAsync("Warning: Error processing frame.");
                        // Optional: Add delay or attempt recovery
                        await Task.Delay(500, stoppingToken);
                    }

                    // Control frame rate - Adjust delay as needed.
                    // This delay aims for roughly 15-20 FPS depending on processing time.
                    // Remove or adjust if CameraService handles timing internally or if higher FPS needed.
                    await Task.Delay(50, stoppingToken); // ~20 FPS target loop rate
                }
                catch (OperationCanceledException)
                {
                    // Expected when stoppingToken is cancelled
                    _logger.LogInformation("Processing loop cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in CameraProcessingService loop.");
                    await SendStatusAsync($"Error: {ex.Message}");
                    // Optional: Implement retry logic or stop processing on repeated errors
                    await Task.Delay(1000, stoppingToken); // Delay before next attempt after error
                }
            }

            _logger.LogInformation("CameraProcessingService processing loop finished.");
            await SendStatusAsync("Processing stopped.");
            // Ensure camera service resources are potentially cleaned if processing stops but app continues
            // _cameraService.StopProcessing(); // Already called or loop exited because flag is false
        }

        private async Task SendStatusAsync(string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveStatus", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update via SignalR.");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraProcessingService stopping.");
            // Ensure camera resources are released when the service stops
            // The Dispose method of CameraService (called via DI container disposal) should handle this.
            // _cameraService.StopProcessing(); // Ensure flag is false
            _cameraService.Dispose(); // Explicitly dispose if necessary, but DI should handle it.

            await base.StopAsync(stoppingToken);
            _logger.LogInformation("CameraProcessingService stopped.");
        }
    }
}
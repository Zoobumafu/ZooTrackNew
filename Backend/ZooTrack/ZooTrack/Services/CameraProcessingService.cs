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
                try
                {
                    await Task.Delay(1000, stoppingToken); // Check every second
                    await SendStatusAsync("Idle. Waiting for start command.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Delay cancelled while waiting for start signal.");
                    break; // Exit if cancellation requested during wait
                }
            }

            // Check if loop exited due to cancellation before processing started
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("CameraProcessingService stopped before processing could start.");
                return;
            }

            _logger.LogInformation("CameraProcessingService received start signal. Beginning processing loop.");
            await SendStatusAsync("Processing starting...");

            // Initial check for initialization before entering the main loop
            if (!_cameraService.IsInitialized)
            {
                _logger.LogWarning("Attempting lazy initialization of CameraService before main loop...");
                if (!_cameraService.InitializeCameraAndYolo())
                {
                    _logger.LogError("Initialization failed in background service before main loop. Stopping.");
                    await SendStatusAsync("Error: Failed to initialize camera or YOLO model.");
                    return; // Stop the service if init fails
                }
                else
                {
                    _logger.LogInformation("Lazy initialization successful before main loop.");
                }
            }

            // ------------- Main processing loop -------------
            _logger.LogInformation("CameraProcessingService starting the WHILE loop.");

            while (!stoppingToken.IsCancellationRequested && _cameraService.IsProcessing)
            {
                _logger.LogInformation("Entered WHILE loop iteration.");

                // Check initialization at the start of each iteration
                if (!_cameraService.IsInitialized)
                {
                    _logger.LogWarning("Processing loop running but camera service not initialized. Attempting re-init...");
                    await SendStatusAsync("Error: Service not initialized. Retrying...");
                    if (!_cameraService.InitializeCameraAndYolo())
                    {
                        _logger.LogError("Re-initialization failed. Stopping processing cycle for now.");
                        await SendStatusAsync("Error: Initialization failed.");
                        _logger.LogInformation("Skipping frame processing due to re-initialization failure.");
                        await Task.Delay(2000, stoppingToken); // Wait a bit longer after re-init failure
                        continue; // Skip to next loop iteration check
                    }
                    else
                    {
                        _logger.LogInformation("Re-initialization successful.");
                        await SendStatusAsync("Initialization successful. Resuming processing.");
                    }
                }

                try
                {
                    _logger.LogInformation("Calling ProcessFrameAsync...");
                    
                    // FIX: Properly await the async method call
                    var frameData = await _cameraService.ProcessFrameAsync();
                    
                    _logger.LogInformation("ProcessFrameAsync returned.");

                    if (frameData != null && frameData.JpegBytes.Length > 0)
                    {
                        _logger.LogInformation("Frame data received ({BytesLength} bytes), sending to clients...", frameData.JpegBytes.Length);
                        await _hubContext.Clients.All.SendAsync("ReceiveFrame", frameData.JpegBytes, stoppingToken);
                        _logger.LogInformation("Frame sent to clients.");

                        // Optionally send detection info
                        if (frameData.TargetDetected)
                        {
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
                        _logger.LogWarning("ProcessFrameAsync returned null. Sending warning status. Check CameraService logs.");
                        await SendStatusAsync("Warning: Error processing frame.");
                        // Optional: Add delay or attempt recovery
                        await Task.Delay(500, stoppingToken); // Delay after null frame
                    }
                    else // Handle case where JpegBytes might be empty
                    {
                        _logger.LogWarning("ProcessFrameAsync returned valid object but JpegBytes were empty.");
                        await Task.Delay(50, stoppingToken); // Small delay even if empty frame data
                    }

                    // Control frame rate - Adjust delay as needed.
                    _logger.LogDebug("Loop iteration complete, delaying for frame rate control...");
                    await Task.Delay(50, stoppingToken); // ~20 FPS target loop rate
                }
                catch (OperationCanceledException)
                {
                    // Expected when stoppingToken is cancelled
                    _logger.LogInformation("Processing loop cancelled via token.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in CameraProcessingService loop processing block. Sending error status.");
                    await SendStatusAsync($"Error: {ex.Message}");
                    // Optional: Implement retry logic or stop processing on repeated errors
                    await Task.Delay(1000, stoppingToken); // Delay before next attempt after error
                }
                _logger.LogDebug("End of WHILE loop iteration.");
            } // End while

            _logger.LogInformation("Exited WHILE loop. Reason: CancellationRequested={CancelStatus}, IsProcessing={ProcessStatus}", 
                stoppingToken.IsCancellationRequested, _cameraService.IsProcessing);

            await SendStatusAsync("Processing stopped.");
        }

        private async Task SendStatusAsync(string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveStatus", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update via SignalR: {Status}", message);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraProcessingService stopping.");
            _cameraService.Dispose(); // Explicitly dispose if necessary
            await base.StopAsync(stoppingToken);
            _logger.LogInformation("CameraProcessingService stopped.");
        }
    }
}
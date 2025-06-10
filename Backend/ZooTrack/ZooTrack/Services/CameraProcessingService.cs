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
    /// <summary>
    /// Background service responsible for continuous camera frame processing and real-time 
    /// broadcasting of processed frames to connected clients via SignalR.
    /// Handles camera initialization, YOLO object detection, and graceful error recovery.
    /// </summary>
    public class CameraProcessingService : BackgroundService
    {
        #region Private Fields

        private readonly ILogger<CameraProcessingService> _logger;
        private readonly CameraService _cameraService;
        private readonly IHubContext<CameraHub> _hubContext;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the CameraProcessingService class.
        /// </summary>
        /// <param name="logger">Logger instance for recording service activities and errors</param>
        /// <param name="cameraService">Singleton camera service for frame processing and YOLO detection</param>
        /// <param name="hubContext">SignalR hub context for broadcasting frames and status updates to clients</param>
        public CameraProcessingService(
            ILogger<CameraProcessingService> logger,
            CameraService cameraService,
            IHubContext<CameraHub> hubContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        #endregion

        #region BackgroundService Implementation

        /// <summary>
        /// Main execution method for the background service. Manages the complete lifecycle
        /// of camera processing including waiting for start signal, initialization, and
        /// continuous frame processing loop.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to signal service shutdown</param>
        /// <returns>Task representing the asynchronous operation</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraProcessingService starting.");

            // Register cancellation callback for clean logging
            stoppingToken.Register(() =>
                _logger.LogInformation("CameraProcessingService stopping token invoked."));

            try
            {
                // Wait for processing to be explicitly started
                await WaitForStartSignalAsync(stoppingToken);

                // Check if service was cancelled before processing could begin
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("CameraProcessingService stopped before processing could start.");
                    return;
                }

                // Ensure camera service is initialized before main loop
                await EnsureInitializationAsync();

                // Execute the main processing loop
                await ExecuteProcessingLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("CameraProcessingService execution was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CameraProcessingService execution.");
                await SendStatusAsync($"Critical Error: {ex.Message}");
            }
            finally
            {
                await SendStatusAsync("Processing stopped.");
                _logger.LogInformation("CameraProcessingService execution completed.");
            }
        }

        /// <summary>
        /// Gracefully stops the camera processing service and disposes of resources.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token for the stop operation</param>
        /// <returns>Task representing the asynchronous stop operation</returns>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraProcessingService stopping.");

            try
            {
                _cameraService.Dispose();
                await base.StopAsync(stoppingToken);

                _logger.LogInformation("CameraProcessingService stopped successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while stopping CameraProcessingService.");
                throw;
            }
        }

        #endregion

        #region Private Processing Methods

        /// <summary>
        /// Waits for the camera service processing to be explicitly started via API or other means.
        /// Sends periodic status updates while waiting.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to interrupt waiting</param>
        /// <returns>Task representing the asynchronous wait operation</returns>
        private async Task WaitForStartSignalAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Waiting for start signal...");

            while (!stoppingToken.IsCancellationRequested && !_cameraService.IsProcessing)
            {
                try
                {
                    await Task.Delay(1000, stoppingToken);
                    await SendStatusAsync("Idle. Waiting for start command.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Wait for start signal was cancelled.");
                    break;
                }
            }
        }

        /// <summary>
        /// Ensures the camera service is properly initialized before beginning frame processing.
        /// Performs lazy initialization if necessary.
        /// </summary>
        /// <returns>Task representing the asynchronous initialization check</returns>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails</exception>
        private async Task EnsureInitializationAsync()
        {
            if (!_cameraService.IsInitialized)
            {
                _logger.LogWarning("Attempting lazy initialization of CameraService...");
                await SendStatusAsync("Initializing camera and YOLO model...");

                if (!_cameraService.InitializeCameraAndYolo())
                {
                    const string errorMessage = "Failed to initialize camera or YOLO model";
                    _logger.LogError(errorMessage);
                    await SendStatusAsync($"Error: {errorMessage}");
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogInformation("Lazy initialization successful.");
                await SendStatusAsync("Initialization complete. Starting processing...");
            }
        }

        /// <summary>
        /// Executes the main processing loop that continuously captures, processes, and broadcasts frames.
        /// Handles initialization checks, error recovery, and frame rate control.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop the processing loop</param>
        /// <returns>Task representing the asynchronous processing loop</returns>
        private async Task ExecuteProcessingLoopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting main processing loop...");
            await SendStatusAsync("Processing starting...");

            while (!stoppingToken.IsCancellationRequested && _cameraService.IsProcessing)
            {
                _logger.LogDebug("Processing loop iteration started.");

                try
                {
                    // Ensure service remains initialized throughout processing
                    await CheckAndRecoverInitializationAsync(stoppingToken);

                    // Process frame and broadcast to clients
                    await ProcessAndBroadcastFrameAsync(stoppingToken);

                    // Control frame rate (~20 FPS target)
                    await Task.Delay(50, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Processing loop cancelled via token.");
                    break;
                }
                catch (Exception ex)
                {
                    await HandleProcessingErrorAsync(ex, stoppingToken);
                }

                _logger.LogDebug("Processing loop iteration completed.");
            }

            _logger.LogInformation("Exited processing loop. CancellationRequested={CancelStatus}, IsProcessing={ProcessStatus}",
                stoppingToken.IsCancellationRequested, _cameraService.IsProcessing);
        }

        /// <summary>
        /// Checks if the camera service is still initialized and attempts recovery if not.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token for the recovery operation</param>
        /// <returns>Task representing the asynchronous initialization check and recovery</returns>
        private async Task CheckAndRecoverInitializationAsync(CancellationToken stoppingToken)
        {
            if (!_cameraService.IsInitialized)
            {
                _logger.LogWarning("Camera service not initialized during processing. Attempting recovery...");
                await SendStatusAsync("Error: Service not initialized. Retrying...");

                if (!_cameraService.InitializeCameraAndYolo())
                {
                    _logger.LogError("Re-initialization failed during processing loop.");
                    await SendStatusAsync("Error: Initialization failed.");
                    await Task.Delay(2000, stoppingToken);
                    return;
                }

                _logger.LogInformation("Re-initialization successful during processing loop.");
                await SendStatusAsync("Initialization successful. Resuming processing.");
            }
        }

        /// <summary>
        /// Processes a single camera frame and broadcasts it to all connected clients.
        /// Also handles detection status updates and error conditions.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token for the frame processing operation</param>
        /// <returns>Task representing the asynchronous frame processing and broadcast</returns>
        private async Task ProcessAndBroadcastFrameAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Processing frame...");

            var frameData = await _cameraService.ProcessFrameAsync();

            if (frameData?.JpegBytes?.Length > 0)
            {
                await BroadcastFrameDataAsync(frameData, stoppingToken);
            }
            else if (frameData == null)
            {
                _logger.LogWarning("ProcessFrameAsync returned null. Check CameraService logs.");
                await SendStatusAsync("Warning: Error processing frame.");
                await Task.Delay(500, stoppingToken);
            }
            else
            {
                _logger.LogWarning("ProcessFrameAsync returned empty frame data.");
                await Task.Delay(50, stoppingToken);
            }
        }

        /// <summary>
        /// Broadcasts processed frame data to all connected clients and sends appropriate status updates.
        /// </summary>
        /// <param name="frameData">The processed frame data containing JPEG bytes and detection information</param>
        /// <param name="stoppingToken">Cancellation token for the broadcast operation</param>
        /// <returns>Task representing the asynchronous broadcast operation</returns>
        private async Task BroadcastFrameDataAsync(object frameData, CancellationToken stoppingToken)
        {
            // Cast to dynamic to access properties, but use specific types for method calls
            dynamic frame = frameData;
            byte[] jpegBytes = frame.JpegBytes;
            bool targetDetected = frame.TargetDetected;

            _logger.LogDebug("Broadcasting frame data ({BytesLength} bytes) to clients...", jpegBytes.Length);

            // Send frame to all connected clients
            await _hubContext.Clients.All.SendAsync("ReceiveFrame", jpegBytes, stoppingToken);

            // Send appropriate status based on detection results
            if (targetDetected)
            {
                string[] detectedTargets = frame.DetectedTargets;
                string detectedTargetsString = string.Join(", ", detectedTargets);
                await SendStatusAsync($"Processing... Target detected: {detectedTargetsString}");
                _logger.LogDebug("Frame broadcast completed with detections: {Targets}", detectedTargetsString);
            }
            else
            {
                await SendStatusAsync("Processing... Monitoring...");
                _logger.LogDebug("Frame broadcast completed - no detections.");
            }
        }

        /// <summary>
        /// Handles errors that occur during the processing loop with appropriate logging and recovery delay.
        /// </summary>
        /// <param name="ex">The exception that occurred during processing</param>
        /// <param name="stoppingToken">Cancellation token for the error handling delay</param>
        /// <returns>Task representing the asynchronous error handling</returns>
        private async Task HandleProcessingErrorAsync(Exception ex, CancellationToken stoppingToken)
        {
            _logger.LogError(ex, "Unhandled exception in processing loop.");
            await SendStatusAsync($"Error: {ex.Message}");

            // Delay before next attempt to prevent rapid error loops
            await Task.Delay(1000, stoppingToken);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Sends a status message to all connected clients via SignalR.
        /// Includes error handling to prevent status update failures from affecting main processing.
        /// </summary>
        /// <param name="message">The status message to broadcast to clients</param>
        /// <returns>Task representing the asynchronous status broadcast</returns>
        private async Task SendStatusAsync(string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveStatus", message);
                _logger.LogDebug("Status sent: {Status}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update via SignalR: {Status}", message);
            }
        }

        #endregion
    }
}
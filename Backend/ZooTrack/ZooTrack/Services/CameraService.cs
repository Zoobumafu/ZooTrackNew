// REWRITTEN AND FIXED
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading.Tasks;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using ZooTrack.Data;
using ZooTrack.Models;
using Timer = System.Timers.Timer;

namespace ZooTrack.Services
{
    /// <summary>
    /// Represents and manages a single physical camera instance, including its capture, processing, and recording resources.
    /// </summary>
    public class CameraInstance : IDisposable
    {
        public int CameraId { get; }
        public bool IsProcessing { get; set; } = false;
        public bool IsInitialized { get; private set; } = false;
        public List<string> TargetAnimals { get; set; } = new List<string>();
        public string HighlightSavePath { get; set; } = "";

        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private VideoCapture? _capture;
        private Yolo? _yolo;
        private VideoWriter? _writer;
        private Timer? _recordingTimer;
        private bool _isRecording = false;
        private readonly TimeSpan _highlightDuration = TimeSpan.FromSeconds(10);
        private int _width = 640;
        private int _height = 480;
        private double _fps = 20.0;
        private bool _disposed = false;

        public CameraInstance(int cameraId, ILogger logger, IServiceScopeFactory serviceScopeFactory)
        {
            CameraId = cameraId;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public bool Initialize()
        {
            if (IsInitialized) return true;
            try
            {
                _logger.LogInformation("Initializing camera {CameraId}...", CameraId);
                // Try opening the camera. Some drivers work better with specific APIs.
                _capture = new VideoCapture(CameraId, VideoCaptureAPIs.DSHOW);
                if (!_capture.IsOpened())
                {
                    _logger.LogWarning("DSHOW failed for camera {CameraId}. Trying default API.", CameraId);
                    _capture.Dispose();
                    _capture = new VideoCapture(CameraId);
                }

                if (!_capture.IsOpened())
                {
                    _logger.LogError("FATAL: Could not open camera {CameraId} with any available API.", CameraId);
                    return false;
                }

                _capture.FrameWidth = _width;
                _capture.FrameHeight = _height;
                _width = _capture.FrameWidth;   // Read back the actual width/height
                _height = _capture.FrameHeight;
                _fps = _capture.Get(VideoCaptureProperties.Fps);
                if (_fps <= 0) _fps = 20.0; // Default FPS if property is not available
                _logger.LogInformation("Camera {CameraId} opened successfully. Resolution: {Width}x{Height}, FPS: {Fps}", CameraId, _width, _height, _fps);

                string modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "yolov10.onnx");
                if (!File.Exists(modelPath))
                {
                    _logger.LogError("YOLO model not found at {ModelPath}. Cannot initialize.", modelPath);
                    return false;
                }
                _yolo = new Yolo(new YoloOptions { OnnxModel = modelPath, ModelType = ModelType.ObjectDetection, Cuda = false });

                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during camera {CameraId} initialization.", CameraId);
                Dispose(); // Clean up resources on failure
                return false;
            }
        }

        public FrameData ProcessFrame()
        {
            if (!IsProcessing || !IsInitialized || _capture == null)
            {
                // If not processing, return a placeholder frame indicating the idle state.
                return CreateErrorFrame("Idle");
            }

            using var frame = new Mat();
            if (!_capture.Read(frame) || frame.Empty())
            {
                // *** MAJOR FIX ***
                // Instead of returning null, create and send a visible error frame to the client.
                _logger.LogWarning("Could not read frame from CameraId: {CameraId}. The camera may be disconnected or in use.", CameraId);
                return CreateErrorFrame("Cannot Read Frame");
            }

            bool targetFound = false;
            var detectedTargets = new List<string>();

            try
            {
                // Run detection logic
                if (_yolo != null)
                {
                    Cv2.ImEncode(".jpg", frame, out byte[] rawData);
                    using var skImage = SKImage.FromEncodedData(rawData);
                    if (skImage != null)
                    {
                        var results = _yolo.RunObjectDetection(skImage, confidence: 0.4f);
                        foreach (var detection in results)
                        {
                            string label = detection.Label.Name.ToLowerInvariant();
                            detectedTargets.Add(label);
                            if (TargetAnimals.Contains(label))
                            {
                                targetFound = true;
                                Task.Run(() => WriteDetectionToDatabase(detection, rawData));
                            }
                            DrawDetection(frame, detection);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during YOLO detection for CameraId: {CameraId}", CameraId);
            }

            HandleHighlightRecording(frame, targetFound);
            Cv2.ImEncode(".jpg", frame, out byte[] finalJpegBytes);

            return new FrameData
            {
                CameraId = this.CameraId,
                JpegBytes = finalJpegBytes,
                TargetDetected = targetFound,
                DetectedTargets = detectedTargets
            };
        }

        private FrameData CreateErrorFrame(string message)
        {
            using var errorMat = new Mat(new Size(_width, _height), MatType.CV_8UC3, Scalar.Black);
            Cv2.PutText(errorMat, $"Camera {CameraId}: {message}", new Point(10, _height / 2), HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);
            Cv2.ImEncode(".jpg", errorMat, out byte[] jpegBytes);
            return new FrameData { CameraId = CameraId, JpegBytes = jpegBytes };
        }

        private void DrawDetection(Mat frame, YoloDotNet.Models.ObjectDetection detection)
        {
            var rect = detection.BoundingBox;
            Cv2.Rectangle(frame, new Rect(rect.Left, rect.Top, rect.Width, rect.Height), Scalar.LimeGreen, 2);
            string text = $"{detection.Label.Name} ({detection.Confidence:P0})";
            Cv2.PutText(frame, text, new Point(rect.Left, rect.Top - 5), HersheyFonts.HersheySimplex, 0.5, Scalar.White, 2);
        }

        private void HandleHighlightRecording(Mat frame, bool targetFoundInFrame)
        {
            if (!targetFoundInFrame || string.IsNullOrEmpty(HighlightSavePath)) return;

            if (!_isRecording)
            {
                StartHighlightRecording();
            }

            // Keep writing to the current video file
            if (_isRecording)
            {
                _writer?.Write(frame);
                // Reset the timer to extend the recording duration
                _recordingTimer?.Stop();
                _recordingTimer?.Start();
            }
        }

        private void StartHighlightRecording()
        {
            if (_isRecording) return;
            _isRecording = true;

            try
            {
                string dir = Path.Combine(HighlightSavePath, $"Camera_{CameraId}");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"highlight_{DateTime.Now:yyyyMMdd_HHmmss}.avi");

                _writer = new VideoWriter(path, FourCC.MJPG, _fps, new Size(_width, _height));
                if (!_writer.IsOpened())
                {
                    _logger.LogError("Could not open VideoWriter for CameraId {CameraId}", CameraId);
                    _isRecording = false;
                    return;
                }

                _logger.LogInformation("Started recording highlight for CameraId {CameraId} to {Path}", CameraId, path);

                _recordingTimer?.Dispose();
                _recordingTimer = new Timer(_highlightDuration.TotalMilliseconds) { AutoReset = false };
                _recordingTimer.Elapsed += (s, e) => StopHighlightRecording();
                _recordingTimer.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start highlight recording for CameraId {CameraId}", CameraId);
                _isRecording = false;
            }
        }

        private void StopHighlightRecording()
        {
            if (!_isRecording) return;

            _logger.LogInformation("Stopping highlight recording for CameraId {CameraId}", CameraId);
            _isRecording = false;
            _recordingTimer?.Stop();
            _recordingTimer?.Dispose();
            _writer?.Release();
            _writer?.Dispose();
            _writer = null;
        }

        // Database logic remains the same...
        private async Task WriteDetectionToDatabase(YoloDotNet.Models.ObjectDetection detection, byte[] frameBytes)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var detectionService = scope.ServiceProvider.GetRequiredService<IDetectionService>();
                var context = scope.ServiceProvider.GetRequiredService<ZootrackDbContext>();

                // Simplified DB logic
                var newDetection = new Detection
                {
                    DeviceId = 1, // This should eventually map to a real device ID
                    DetectedAt = DateTime.Now,
                    DetectedObject = detection.Label.Name,
                    Confidence = (float)(detection.Confidence * 100),
                    // Bounding box info...
                };
                await detectionService.CreateDetectionAsync(newDetection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write detection to database for CameraId: {CameraId}", CameraId);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsProcessing = false;
            IsInitialized = false;
            StopHighlightRecording();
            _capture?.Release();
            _capture?.Dispose();
            _yolo?.Dispose();
            _logger.LogInformation("Disposed resources for CameraInstance {CameraId}", CameraId);
        }
    }

    /// <summary>
    /// Manages the lifecycle of all CameraInstance objects.
    /// </summary>
    public class CameraService : IDisposable
    {
        private readonly ILogger<CameraService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentDictionary<int, CameraInstance> _cameraInstances = new();

        public CameraService(ILogger<CameraService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public List<(int CameraId, string Name, bool IsActive)> DiscoverCameras()
        {
            var cameras = new List<(int, string, bool)>();
            _logger.LogInformation("Starting camera discovery...");
            for (int i = 0; i < 10; i++) // Check first 10 indices
            {
                try
                {
                    using var capture = new VideoCapture(i);
                    if (capture.IsOpened())
                    {
                        cameras.Add((i, $"Camera {i}", true));
                        _logger.LogInformation("Found accessible camera at index {Index}", i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A critical error (likely a driver crash) occurred while probing for camera at index {Index}. Skipping.", i);
                }
            }
            return cameras;
        }

        public bool IsCameraInitialized(int cameraId) => _cameraInstances.ContainsKey(cameraId) && _cameraInstances[cameraId].IsInitialized;

        public bool InitializeCamera(int cameraId)
        {
            // Ensure only one instance is created per ID
            var instance = _cameraInstances.GetOrAdd(cameraId, id => new CameraInstance(id, _logger, _serviceScopeFactory));

            if (!instance.IsInitialized)
            {
                return instance.Initialize();
            }
            return true;
        }

        public void StartProcessing(int cameraId, List<string> targetAnimals, string savePath)
        {
            if (_cameraInstances.TryGetValue(cameraId, out var instance) && instance.IsInitialized)
            {
                instance.TargetAnimals = targetAnimals ?? new List<string>();
                instance.HighlightSavePath = savePath ?? "";
                instance.IsProcessing = true;
                _logger.LogInformation("Processing started for CameraId: {CameraId}", cameraId);
            }
            else
            {
                _logger.LogWarning("Attempted to start processing for uninitialized or non-existent CameraId: {CameraId}", cameraId);
            }
        }

        public void StopProcessing(int cameraId)
        {
            if (_cameraInstances.TryGetValue(cameraId, out var instance))
            {
                instance.IsProcessing = false;
                _logger.LogInformation("Processing stopped for CameraId: {CameraId}", cameraId);
            }
        }

        public void StopAllProcessing()
        {
            foreach (var instance in _cameraInstances.Values)
            {
                instance.IsProcessing = false;
            }
            _logger.LogInformation("All camera processing stopped.");
        }

        public FrameData? ProcessFrame(int cameraId)
        {
            if (_cameraInstances.TryGetValue(cameraId, out var instance))
            {
                return instance.ProcessFrame();
            }
            return null; // Should ideally not happen if called correctly
        }

        public List<int> GetActiveCameraIds()
        {
            return _cameraInstances.Where(kvp => kvp.Value.IsProcessing).Select(kvp => kvp.Key).ToList();
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing CameraService and all camera instances.");
            foreach (var instance in _cameraInstances.Values)
            {
                instance.Dispose();
            }
            _cameraInstances.Clear();
        }
    }

    public class FrameData
    {
        public int CameraId { get; set; }
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();
        public bool TargetDetected { get; set; } = false;
        public List<string> DetectedTargets { get; set; } = new List<string>();
    }
}
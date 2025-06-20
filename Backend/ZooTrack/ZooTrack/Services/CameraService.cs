using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using ZooTrack.Data;
using ZooTrack.Models;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Timers.Timer;

namespace ZooTrack.Services
{
    // Helper class to hold all resources and state for a single camera
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
        private readonly TimeSpan _highlightDuration = TimeSpan.FromSeconds(5);
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
                _logger.LogInformation("Initializing CameraInstance for CameraId: {CameraId}", CameraId);
                _capture = new VideoCapture(CameraId, VideoCaptureAPIs.DSHOW);
                if (!_capture.IsOpened())
                {
                    _capture.Dispose();
                    _capture = new VideoCapture(CameraId);
                    if (!_capture.IsOpened())
                    {
                        _logger.LogError("Error: Could not open camera for CameraId: {CameraId}", CameraId);
                        return false;
                    }
                }

                _capture.FrameWidth = _width;
                _capture.FrameHeight = _height;
                _width = _capture.FrameWidth;
                _height = _capture.FrameHeight;
                _fps = _capture.Get(VideoCaptureProperties.Fps);
                if (_fps <= 0) _fps = 20.0;

                string modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "yolov10.onnx");
                if (!File.Exists(modelPath))
                {
                    _logger.LogError("YOLO model not found at {ModelPath}", modelPath);
                    _capture.Release();
                    return false;
                }
                _yolo = new Yolo(new YoloOptions { OnnxModel = modelPath, ModelType = ModelType.ObjectDetection, Cuda = false });

                IsInitialized = true;
                _logger.LogInformation("Successfully initialized CameraInstance for CameraId: {CameraId}", CameraId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize CameraInstance for CameraId: {CameraId}", CameraId);
                Dispose();
                return false;
            }
        }

        public FrameData? ProcessFrame()
        {
            if (!IsProcessing || !IsInitialized || _capture == null || _yolo == null || _capture.IsDisposed) return null;

            using var frame = new Mat();
            if (!_capture.Read(frame) || frame.Empty())
            {
                _logger.LogWarning("Could not read frame from CameraId: {CameraId}", CameraId);
                return null;
            }

            bool targetFound = false;
            var detectedTargets = new List<string>();

            try
            {
                Cv2.ImEncode(".jpg", frame, out byte[] rawData);
                using var skImage = SKImage.FromEncodedData(rawData);
                if (skImage != null)
                {
                    var results = _yolo.RunObjectDetection(skImage, confidence: 0.35f, iou: 0.6f);
                    foreach (var detection in results)
                    {
                        string label = detection.Label.Name.ToLowerInvariant();
                        detectedTargets.Add(label);
                        if (TargetAnimals.Contains(label))
                        {
                            targetFound = true;
                            // Asynchronously write to DB to not block the frame processing
                            Task.Run(() => WriteDetectionToDatabase(detection, rawData));
                        }
                        DrawDetection(frame, detection);
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

        private void DrawDetection(Mat frame, YoloDotNet.Models.ObjectDetection detection)
        {
            var rect = detection.BoundingBox;
            Cv2.Rectangle(frame, new Rect(rect.Left, rect.Top, rect.Width, rect.Height), Scalar.Red, 2);
            string text = $"{detection.Label.Name} {detection.Confidence:P1}";
            Cv2.PutText(frame, text, new Point(rect.Left, rect.Top - 5), HersheyFonts.HersheySimplex, 0.6, Scalar.LimeGreen, 2);
        }

        private async Task WriteDetectionToDatabase(YoloDotNet.Models.ObjectDetection detection, byte[] frameBytes)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ZootrackDbContext>();
                var detectionService = scope.ServiceProvider.GetRequiredService<IDetectionService>();

                string mediaDir = Path.Combine(AppContext.BaseDirectory, "MediaFiles", "Detections");
                Directory.CreateDirectory(mediaDir);
                string fileName = $"detection_{CameraId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
                string filePath = Path.Combine(mediaDir, fileName);
                await File.WriteAllBytesAsync(filePath, frameBytes);

                var media = new Media
                {
                    FilePath = Path.Combine("Detections", fileName),
                    Type = "Image",
                    Timestamp = DateTime.Now,
                    DeviceId = 1 // Link to the 'physical device' record in DB.
                };
                context.Media.Add(media);
                await context.SaveChangesAsync();

                var detectionRecord = new Detection
                {
                    DeviceId = 1, // Foreign key to Device table
                    MediaId = media.MediaId,
                    Confidence = (float)(detection.Confidence * 100),
                    DetectedAt = DateTime.Now,
                    DetectedObject = detection.Label.Name,
                    BoundingBoxX = detection.BoundingBox.Left,
                    BoundingBoxY = detection.BoundingBox.Top,
                    BoundingBoxWidth = detection.BoundingBox.Width,
                    BoundingBoxHeight = detection.BoundingBox.Height,
                };

                await detectionService.CreateDetectionAsync(detectionRecord);
                _logger.LogInformation("Detection from CameraId {CamId} saved for {Label}", CameraId, detection.Label.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write detection to database for CameraId: {CameraId}", CameraId);
            }
        }

        private void HandleHighlightRecording(Mat frame, bool targetFoundInFrame)
        {
            if (TargetAnimals.Count == 0 || string.IsNullOrEmpty(HighlightSavePath)) return;
            if (targetFoundInFrame && !_isRecording) StartHighlightRecording();
            if (_isRecording) _writer?.Write(frame);
        }

        private void StartHighlightRecording()
        {
            if (_isRecording) return;
            try
            {
                _isRecording = true;
                string dir = Path.Combine(HighlightSavePath, $"Camera_{CameraId}");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"highlight_{DateTime.Now:yyyyMMdd_HHmmss}.avi");

                _writer = new VideoWriter(path, VideoWriter.FourCC('M', 'J', 'P', 'G'), _fps, new Size(_width, _height));
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

    // Main service that manages multiple CameraInstance objects
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

        public List<(int CameraId, string Name, bool IsActive)> DiscoverCameras()
        {
            var cameras = new List<(int, string, bool)>();
            _logger.LogInformation("Starting camera discovery...");
            for (int i = 0; i < 10; i++) // Check first 10 indices
            {
                using var capture = new VideoCapture(i);
                if (capture.IsOpened())
                {
                    cameras.Add((i, $"Camera {i}", true));
                    _logger.LogInformation("Found camera at index {Index}", i);
                }
            }
            return cameras;
        }

        public bool IsCameraInitialized(int cameraId) => _cameraInstances.ContainsKey(cameraId) && _cameraInstances[cameraId].IsInitialized;

        public bool InitializeCamera(int cameraId)
        {
            var instance = new CameraInstance(cameraId, _logger, _serviceScopeFactory);
            if (instance.Initialize())
            {
                _cameraInstances[cameraId] = instance;
                return true;
            }
            return false;
        }

        public void StartProcessing(int cameraId, List<string> targetAnimals, string savePath)
        {
            if (_cameraInstances.TryGetValue(cameraId, out var instance))
            {
                instance.TargetAnimals = targetAnimals;
                instance.HighlightSavePath = savePath;
                instance.IsProcessing = true;
                _logger.LogInformation("Processing started for CameraId: {CameraId}", cameraId);
            }
            else
            {
                _logger.LogWarning("Attempted to start processing for uninitialized CameraId: {CameraId}", cameraId);
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
            return null;
        }

        public List<int> GetActiveCameraIds()
        {
            return _cameraInstances.Where(kvp => kvp.Value.IsProcessing).Select(kvp => kvp.Key).ToList();
        }

        public Dictionary<int, (bool IsProcessing, bool IsInitialized)> GetAllStatuses()
        {
            return _cameraInstances.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value.IsProcessing, kvp.Value.IsInitialized));
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

    // FrameData now includes CameraId
    public class FrameData
    {
        public int CameraId { get; set; }
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();
        public bool TargetDetected { get; set; } = false;
        public List<string> DetectedTargets { get; set; } = new List<string>();
    }
}
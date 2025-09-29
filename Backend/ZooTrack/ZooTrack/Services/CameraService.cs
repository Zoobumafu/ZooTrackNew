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
    /// contains two classes: 
    ///     CameraInsrance for managing single camera instance ans
    ///     CameraService for managing all the cameras
    /// this service uses re-trained YOLOv10 model for process video, saves highlight and draw B-Boxes around them
    /// 
    /// Responsibilities:
    ///     1. Set and connect to the cameras
    ///     2. Object Detection by Yolo
    ///     3. Record Highlights
    ///     4. Write target to DB if it was found
    ///     5. Show client the processed farmed with B-Boxed object
    /// 
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

        // connect to specific camera using OpenCvSharp, load Yolo, set resolution
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

        // TODO: check method responsibilities
        // MAIN METHOD: process frames
        // 1. Reads from camera and run Yolo
        // 2. Calls to Draws B-Box and labels
        // 3. Saves to DB: frame & frame metadata
        public FrameData ProcessFrame()
        {
            // IsProcessing = camera is actively being monitored
            // IsInitialized = camera connection and YOLO model are ready
            // _capture != null = camera object exists
            if (!IsProcessing || !IsInitialized || _capture == null)
            {
                return CreateErrorFrame("Idle");
            }

            using var frame = new Mat();
            if (!_capture.Read(frame) || frame.Empty())
            {
                _logger.LogWarning("Error! Could not read frame from CameraId: {CameraId}. Device may be disconnected or in use.", CameraId);
                return CreateErrorFrame("Cannot Read Frame");
            }

            bool targetFound = false;
            var detectedTargets = new List<string>();
            var allDetections = new List<YoloDotNet.Models.ObjectDetection>();

            try
            {
                // Run detection logic
                if (_yolo != null)
                {
                    Cv2.ImEncode(".jpg", frame, out byte[] rawData); // compress image and saves in buffer
                    using var skImage = SKImage.FromEncodedData(rawData); // load from buffer into Skia object
                    if (skImage != null)
                    {
                        var results = _yolo.RunObjectDetection(skImage, confidence: 0.4f); // run Yolo for obj deetection
                        foreach (var detectedAnimal in results)
                        {
                            // check if the found obj is in target list
                            string label = detectedAnimal.Label.Name.ToLowerInvariant();
                            bool isTarget = TargetAnimals.Contains(label);
                            DrawDetection(frame, detectedAnimal, isTarget);

                            if (isTarget)
                            {
                                detectedTargets.Add(label);

                                // Queue for the DetectionMediaService to handle
                                _ = Task.Run(async () =>
                                {
                                    var tempDetection = new Detection
                                    {
                                        DeviceId = CameraId,
                                        DetectedAt = DateTime.Now,
                                        DetectedObject = detectedAnimal.Label.Name,
                                        BoundingBoxX = detectedAnimal.BoundingBox.Left,
                                        BoundingBoxY = detectedAnimal.BoundingBox.Top,
                                        BoundingBoxWidth = detectedAnimal.BoundingBox.Width,
                                        BoundingBoxHeight = detectedAnimal.BoundingBox.Height,
                                        Confidence = (float)(detectedAnimal.Confidence * 100),
                                        IsTarget = isTarget
                                    };
                                    using var scope = _serviceScopeFactory.CreateScope();
                                    var mediaService = scope.ServiceProvider.GetRequiredService<DetectionMediaService>();

                                    // Let DetectionMediaService decide whether to save based on tracking
                                    await mediaService.ProcessDetectionForSaving(tempDetection, rawData);
                                });
                                /*
                                // save to DB
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await WriteDetectionToDatabase(detectedAnimal, rawData, isTarget);
                                        _logger.LogInformation("Target saved: {Label}", label);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Failed to save target: {Label}", label);
                                    }
                                });
                            }
                            else
                            {
                                _logger.LogDebug("Non-target detected: {Label}", label);
                                */
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during YOLO detection for CameraId: {CameraId}", CameraId);
            }

            bool hasTargets = detectedTargets.Count > 0;
            // records 10 seconds when target is found
            if (hasTargets)
                HandleHighlightRecording(frame, hasTargets);

            var jpegQuality = hasTargets ? 95 : 75; // Higher quality for targets
            var jpegParams = new int[] { (int)ImwriteFlags.JpegQuality, jpegQuality };
            Cv2.ImEncode(".jpg", frame, out byte[] finalJpegBytes, jpegParams);

            if (hasTargets)
            {
                _logger.LogInformation("Targets detected in frame from Camera {CameraId}: [{Targets}]",
                    CameraId, string.Join(", ", detectedTargets));
            }
            if (allDetections.Count > 0)
            {
                var allLabels = allDetections.Select(d => d.Label.Name.ToLowerInvariant()).ToArray();
                _logger.LogDebug("All detections in frame from Camera {CameraId}: [{AllDetections}]",
                    CameraId, string.Join(", ", allLabels));
            }

            return new FrameData
            {
                CameraId = this.CameraId,
                JpegBytes = finalJpegBytes,
                TargetDetected = hasTargets,
                DetectedTargets = detectedTargets
            };
        }

        // Error handaling
        private FrameData CreateErrorFrame(string message)
        {
            using var errorMat = new Mat(new Size(_width, _height), MatType.CV_8UC3, Scalar.Black);
            Cv2.PutText(errorMat, $"Camera {CameraId}: {message}", new Point(10, _height / 2), HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);
            Cv2.ImEncode(".jpg", errorMat, out byte[] jpegBytes);
            return new FrameData { CameraId = CameraId, JpegBytes = jpegBytes };
        }

        // Draw B-Box around object in the Camera
        private void DrawDetection(Mat frame, YoloDotNet.Models.ObjectDetection detection, bool isTarget)
        {
            var rect = detection.BoundingBox;

            // draw B-Box
            var color = isTarget ? Scalar.LimeGreen : Scalar.Gray;
            var thickness = isTarget ? 3 : 2;
            var cvRect = new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
            Cv2.Rectangle(frame, cvRect, color, thickness);

            // label
            string text = $"{detection.Label.Name} ({detection.Confidence:P0})";
            double fontScale = isTarget ? 0.6 : 0.5;
            var textColor = isTarget ? Scalar.White : Scalar.LightGray;
            int textThickness = isTarget ? 2 : 1;
            var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, fontScale, textThickness, out int baseline);
            Point textPosition;

            // frame limitation
            if (rect.Top < textSize.Height + 10)
                textPosition = new Point(rect.Left + 5, rect.Top + textSize.Height + 5);
            else
                textPosition = new Point(rect.Left, rect.Top - 5);

            textPosition.X = Math.Max(0, Math.Min(textPosition.X, frame.Width - textSize.Width));
            textPosition.Y = Math.Max(textSize.Height, Math.Min(textPosition.Y, frame.Height - baseline));

            // text background
            if (isTarget)
            {
                var bgRect = new Rect(textPosition.X - 2, textPosition.Y - textSize.Height - 2,
                                     textSize.Width + 4, textSize.Height + baseline + 4);
                Cv2.Rectangle(frame, bgRect, Scalar.Black, -1); // black background
            }

            Cv2.PutText(frame, text, textPosition, HersheyFonts.HersheySimplex,
              fontScale, textColor, textThickness);
        }

        // records video only when target is found
        private void HandleHighlightRecording(Mat frame, bool hasTargetsInFrame)
        {
            if (!hasTargetsInFrame || string.IsNullOrEmpty(HighlightSavePath)) return;

            if (!_isRecording)
                StartHighlightRecording();

            // Keep writing to the current video file
            if (_isRecording)
            {
                _writer?.Write(frame);
                // Reset the timer to extend the recording duration
                _recordingTimer?.Stop();
                _recordingTimer?.Start();
            }
        }

        // help method to HandleHighlightRecording()
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

        // help method to HandleHighlightRecording()
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

        // saves detecion into DB,
        // saves precced frame as photo
        // create record in DB
        // initiate tracking with CreateDetectionWithTrackingAsync()
        private async Task WriteDetectionToDatabase(YoloDotNet.Models.ObjectDetection detection, byte[] frameBytes, bool isTarget)
        {
            _logger.LogInformation(">>> WriteDetectionToDatabase started for Camera {CameraId}, IsTarget={IsTarget}", CameraId, isTarget);
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var detectionService = scope.ServiceProvider.GetRequiredService<IDetectionService>();
                var context = scope.ServiceProvider.GetRequiredService<ZootrackDbContext>();

                _logger.LogInformation("Saving frame as Media...");
                int deviceId = this.CameraId;
                int mediaId = await SaveFrameAsMediaAsync(frameBytes, deviceId, context);
                _logger.LogInformation("Media saved with MediaId={MediaId}", mediaId);

                var newDetection = new Detection
                {
                    DeviceId = deviceId,
                    MediaId = mediaId,
                    DetectedAt = DateTime.Now,
                    DetectedObject = detection.Label.Name,
                    Confidence = (float)(detection.Confidence * 100),
                    IsTarget = isTarget
                };

                _logger.LogInformation("Creating detection via DetectionService...");
                var created = await detectionService.CreateDetectionWithTrackingAsync(
                    newDetection,
                    detection.BoundingBox.Left,
                    detection.BoundingBox.Top,
                    detection.BoundingBox.Width,
                    detection.BoundingBox.Height,
                    detection.Label.Name
                );

                _logger.LogInformation("Detection {DetectionId} saved successfully (IsTarget={IsTarget})", created.DetectionId, isTarget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! WriteDetectionToDatabase failed for Camera {CameraId}", CameraId);
                throw;
            }
        }


        private async Task<int> SaveFrameAsMediaAsync(byte[] frameBytes, int deviceId, ZootrackDbContext context)
        {
            // create path
            string relativePath = Path.Combine("Media/HighlightFrames", $"detection_{DateTime.Now:yyyyMMdd_HHmmss_ff}.jpg");
            string fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            // write into local disk
            await File.WriteAllBytesAsync(fullPath, frameBytes);

            // create Media in DB
            var media = new Media
            {
                DeviceId = deviceId,
                Type = "Frame",
                FilePath = relativePath,
                Timestamp = DateTime.Now
            };

            context.Media.Add(media);
            await context.SaveChangesAsync();

            return media.MediaId;
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

    /// Manages the lifecycle of CameraInstance objects.
    public class CameraService : IDisposable
    {
        private readonly ILogger<CameraService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentDictionary<int, CameraInstance> _cameraInstances = new();

        private readonly List<string> _defaultTargetAnimals;
        public const int NUM_CAMERAS = 2; // for testing porpose

        public CameraService(ILogger<CameraService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _defaultTargetAnimals = LoadDefaultTargetAnimals();
        }

        private List<string> LoadDefaultTargetAnimals()
        {
            try
            {
                string jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "TargetAnimals.json");
                if (!File.Exists(jsonPath))
                {
                    _logger.LogWarning("TargetAnimals.json not found at {Path}, using empty list", jsonPath);
                    return new List<string>();
                }

                string json = File.ReadAllText(jsonPath);
                var animals = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (animals == null || animals.Count == 0)
                {
                    _logger.LogWarning("TargetAnimals.json is empty or invalid, using empty list");
                    return new List<string>();
                }

                _logger.LogInformation("Loaded {Count} default target animals from {Path}", animals.Count, jsonPath);
                return animals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load TargetAnimals.json, using empty list");
                return new List<string>();
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public List<(int CameraId, string Name, bool IsActive)> DiscoverCameras()
        {
            var cameras = new List<(int, string, bool)>();
            _logger.LogInformation("Starting camera discovery...");
            for (int i = 0; i < NUM_CAMERAS; i++)
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

        public bool IsCameraInitialized(int cameraId) =>
            _cameraInstances.ContainsKey(cameraId) && _cameraInstances[cameraId].IsInitialized;

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

        // set targets, set path, initiate processing condition for camera instance
        public void StartProcessing(int cameraId, List<string> targetAnimals, string savePath)
        {
            if (_cameraInstances.TryGetValue(cameraId, out var instance) && instance.IsInitialized)
            {
                instance.TargetAnimals = (targetAnimals == null || targetAnimals.Count == 0)
                    ? _defaultTargetAnimals
                    : targetAnimals;

                instance.HighlightSavePath = savePath ?? "";
                instance.IsProcessing = true;

                _logger.LogInformation(
                    "Processing started for CameraId: {CameraId} with {Count} target animals",
                    cameraId, instance.TargetAnimals.Count);
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

    // Data Transfer Object between Client and Server by SignalIR
    public class FrameData
    {
        public int CameraId { get; set; }
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();
        public bool TargetDetected { get; set; } = false;
        public List<string> DetectedTargets { get; set; } = new List<string>();
    }
}
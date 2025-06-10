// ZooTrack.WebAPI/Services/CameraService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using ZooTrack.Data;
using ZooTrack.Models;
using Timer = System.Timers.Timer;

namespace ZooTrack.Services
{
    /// <summary>
    /// Represents frame processing data containing JPEG bytes and detection information
    /// </summary>
    public class FrameData
    {
        /// JPEG-encoded frame bytes for streaming
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();

        /// Indicates whether any target animal was detected in the frame
        public bool TargetDetected { get; set; } = false;

        /// List of all detected object labels in the frame

        public List<string> DetectedTargets { get; set; } = new List<string>();
    }

    /// Camera service that handles video capture, YOLO object detection, tracking, and highlight recording
    public class CameraService : IDisposable
    {
        #region Fields and Properties

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<CameraService> _logger;

        // Camera and detection components
        private VideoCapture? _capture;
        private Yolo? _yolo;

        // Recording components
        private VideoWriter? _writer;
        private Timer? _recordingTimer;

        // Recording state
        private bool _isRecording = false;
        private DateTime _recordingStartTime;
        private readonly TimeSpan _highlightDuration = TimeSpan.FromSeconds(5);
        private string _highlightSavePath = string.Empty;
        private List<string> _targetAnimals = new List<string>();

        // Camera settings
        private int _width = 640;
        private int _height = 480;
        private double _fps = 20.0;

        // Tracking components
        private TrackerMIL? _tracker;
        private Rect _trackedObjectBox;
        private bool _isTrackingObject = false;

        // Initialization control
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();
        private bool _disposed = false;

        /// Gets whether the camera service has been successfully initialized
        public bool IsInitialized { get; private set; } = false;


        /// Gets whether the camera service is currently processing frames
        public bool IsProcessing { get; private set; } = false;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the CameraService
        /// </summary>
        /// <param name="logger">Logger for capturing service events</param>
        /// <param name="serviceScopeFactory">Factory for creating database service scopes</param>
        public CameraService(ILogger<CameraService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Initializes the camera hardware and YOLO model for object detection
        /// Thread-safe initialization that prevents multiple simultaneous initialization attempts
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public bool InitializeCameraAndYolo()
        {
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    _logger.LogWarning("CameraService already initialized.");
                    return true;
                }

                try
                {
                    _logger.LogInformation("Initializing Camera and YOLO model...");

                    if (!InitializeCamera())
                    {
                        return false;
                    }

                    if (!InitializeYoloModel())
                    {
                        _capture?.Release();
                        return false;
                    }

                    _logger.LogInformation("Camera and YOLO model initialized successfully.");
                    IsInitialized = true;
                    _isInitialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal error during CameraService initialization.");
                    CleanupResources();
                    return false;
                }
            }
        }

        /// <summary>
        /// Initializes the camera hardware with fallback API options
        /// </summary>
        /// <returns>True if camera was successfully opened, false otherwise</returns>
        private bool InitializeCamera()
        {
            _capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (!_capture.Open(0, VideoCaptureAPIs.DSHOW))
            {
                _logger.LogError("Error: Could not open camera using DSHOW. Trying default API...");
                _capture.Dispose();
                _capture = new VideoCapture(0);
                if (!_capture.Open(0))
                {
                    _logger.LogError("Error: Could not open camera using default API.");
                    _capture = null;
                    return false;
                }
            }

            // Configure camera settings
            _capture.FrameWidth = _width;
            _capture.FrameHeight = _height;

            // Read actual camera capabilities
            _width = _capture.FrameWidth;
            _height = _capture.FrameHeight;
            _fps = _capture.Get(VideoCaptureProperties.Fps);

            if (_fps <= 0 || double.IsNaN(_fps) || double.IsInfinity(_fps))
            {
                _logger.LogWarning("Could not get valid FPS from camera. Defaulting to {DefaultFps}", 20.0);
                _fps = 20.0;
            }

            _logger.LogInformation("Camera opened successfully. Resolution: {Width}x{Height}, FPS: {Fps}",
                _width, _height, _fps);
            return true;
        }

        /// <summary>
        /// Initializes the YOLO object detection model
        /// </summary>
        /// <returns>True if YOLO model was successfully loaded, false otherwise</returns>
        private bool InitializeYoloModel()
        {
            string modelDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
            string modelPath = Path.Combine(modelDirectory, "yolov10.onnx");

            if (!Directory.Exists(modelDirectory))
            {
                _logger.LogError("Model directory not found at {ModelDirectory}", modelDirectory);
                return false;
            }

            if (!File.Exists(modelPath))
            {
                _logger.LogError("YOLO model not found at {ModelPath}", modelPath);
                return false;
            }

            _yolo = new Yolo(new YoloOptions
            {
                OnnxModel = modelPath,
                ModelType = ModelType.ObjectDetection,
                Cuda = false // Set true for GPU (ensure CUDA/cuDNN setup)
            });

            _logger.LogInformation("YOLO model loaded successfully.");
            return true;
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Sets the target animals to detect and the path for saving highlight recordings
        /// </summary>
        /// <param name="targetAnimals">List of animal names to trigger recordings</param>
        /// <param name="savePath">Directory path where highlight videos will be saved</param>
        public void SetProcessingTargets(List<string> targetAnimals, string savePath)
        {
            if (!IsInitialized)
            {
                _logger.LogWarning("Cannot set targets: Service not initialized.");
                return;
            }

            _targetAnimals = targetAnimals ?? new List<string>();
            _highlightSavePath = savePath;
            _logger.LogInformation("Processing targets set. Animals: {Animals}. Save Path: {Path}",
                string.Join(", ", _targetAnimals), _highlightSavePath);

            EnsureHighlightDirectoryExists();
        }

        /// <summary>
        /// Creates the highlight directory if it doesn't exist
        /// </summary>
        private void EnsureHighlightDirectoryExists()
        {
            if (!string.IsNullOrEmpty(_highlightSavePath) && _targetAnimals.Count > 0)
            {
                try
                {
                    Directory.CreateDirectory(_highlightSavePath);
                    _logger.LogInformation("Ensured highlight directory exists: {Path}", _highlightSavePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create highlight directory: {Path}", _highlightSavePath);
                }
            }
        }

        #endregion

        #region Frame Processing

        /// <summary>
        /// Processes a single frame from the camera, performing object detection, tracking, and recording
        /// This is the main processing loop method called by the background service
        /// </summary>
        /// <returns>FrameData containing processed frame and detection results, or null if processing failed</returns>
        public async Task<FrameData?> ProcessFrameAsync()
        {
            if (!IsInitialized || _capture == null || !_capture.IsOpened() || _yolo == null)
            {
                _logger.LogWarning("ProcessFrame called but service is not ready.");
                return null;
            }

            using var frame = new Mat();

            // Read frame from camera
            if (!ReadFrameFromCamera(frame))
            {
                return null;
            }

            var processingResult = await ProcessFrameDetection(frame);
            HandleHighlightRecording(frame, processingResult.targetFound);

            // Encode final frame for streaming
            Cv2.ImEncode(".jpg", frame, out byte[] finalJpegBytes);

            return new FrameData
            {
                JpegBytes = finalJpegBytes,
                TargetDetected = processingResult.targetFound,
                DetectedTargets = processingResult.detections
            };
        }

        /// <summary>
        /// Reads a frame from the camera with error handling
        /// </summary>
        /// <param name="frame">Mat object to store the captured frame</param>
        /// <returns>True if frame was successfully read, false otherwise</returns>
        private bool ReadFrameFromCamera(Mat frame)
        {
            try
            {
                if (!_capture!.Read(frame) || frame.Empty())
                {
                    _logger.LogWarning("Could not read frame from camera.");
                    return false;
                }
                return true;
            }
            catch (Exception readEx)
            {
                _logger.LogError(readEx, "Exception while reading frame from camera.");
                return false;
            }
        }

        /// <summary>
        /// Processes object detection and tracking for a frame
        /// </summary>
        /// <param name="frame">The frame to process</param>
        /// <returns>Tuple containing detection results and whether targets were found</returns>
        private async Task<(List<string> detections, bool targetFound)> ProcessFrameDetection(Mat frame)
        {
            var currentFrameDetections = new List<string>();
            bool targetFoundInFrame = false;
            YoloDotNet.Models.ObjectDetection? mainTarget = null;

            try
            {
                // Run YOLO detection
                Cv2.ImEncode(".jpg", frame, out byte[] rawData);
                using var skImage = SKImage.FromEncodedData(rawData);

                if (skImage == null)
                {
                    _logger.LogWarning("Could not decode frame using SkiaSharp.");
                    return (currentFrameDetections, false);
                }

                var results = _yolo!.RunObjectDetection(skImage, confidence: 0.35f, iou: 0.6f);

                // Process detections and draw bounding boxes
                foreach (var detection in results)
                {
                    string label = detection.Label.Name.ToLowerInvariant();
                    currentFrameDetections.Add(detection.Label.Name);

                    if (_targetAnimals.Contains(label))
                    {
                        targetFoundInFrame = true;
                        mainTarget ??= detection;
                    }

                    DrawDetection(frame, detection);
                }

                // Save positive detections to database
                if (targetFoundInFrame && mainTarget != null)
                {
                    await WriteDetectionToDatabase(mainTarget, rawData);
                }

                // Handle object tracking
                ProcessObjectTracking(frame, mainTarget);
            }
            catch (Exception yoloEx)
            {
                _logger.LogError(yoloEx, "Error during YOLO detection or tracking.");
                Cv2.PutText(frame, "Detection Error", new Point(10, 30),
                    HersheyFonts.HersheySimplex, 1.0, Scalar.Magenta, 2);
                ResetTracking();
            }

            return (currentFrameDetections, targetFoundInFrame);
        }

        #endregion

        #region Object Tracking

        /// <summary>
        /// Processes object tracking for the current frame
        /// Initializes tracking for new targets or updates existing tracking
        /// </summary>
        /// <param name="frame">Current frame to process</param>
        /// <param name="mainTarget">Primary target detection to track</param>
        private void ProcessObjectTracking(Mat frame, YoloDotNet.Models.ObjectDetection? mainTarget)
        {
            // Initialize tracker if a target is found and not already tracking
            if (mainTarget != null && !_isTrackingObject)
            {
                InitializeObjectTracker(frame, mainTarget);
            }

            // Update existing tracker
            if (_isTrackingObject && _tracker != null)
            {
                UpdateObjectTracker(frame);
            }
        }

        /// <summary>
        /// Initializes object tracking for a detected target
        /// </summary>
        /// <param name="frame">Frame containing the target</param>
        /// <param name="initialDetection">Detection result for the target to track</param>
        /// <returns>True if tracker was successfully initialized, false otherwise</returns>
        private bool InitializeObjectTracker(Mat frame, YoloDotNet.Models.ObjectDetection initialDetection)
        {
            try
            {
                var rect = initialDetection.BoundingBox;
                _trackedObjectBox = new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
                _tracker = TrackerMIL.Create();
                _tracker.Init(frame, _trackedObjectBox);
                _isTrackingObject = true;
                _logger.LogInformation("Object tracker initialized for {Label}", initialDetection.Label.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize object tracker.");
                return false;
            }
        }

        /// <summary>
        /// Updates the existing object tracker with the current frame
        /// </summary>
        /// <param name="frame">Current frame to update tracking</param>
        private void UpdateObjectTracker(Mat frame)
        {
            Rect currentTrackedBox = _trackedObjectBox;
            bool tracked = _tracker!.Update(frame, ref currentTrackedBox);
            _trackedObjectBox = currentTrackedBox;

            if (tracked)
            {
                Cv2.Rectangle(frame, _trackedObjectBox, Scalar.Cyan, 2);
            }
            else
            {
                _logger.LogWarning("Tracking failed. Re-enabling YOLO detection.");
                ResetTracking();
            }
        }

        /// <summary>
        /// Resets object tracking state and disposes of tracker resources
        /// </summary>
        private void ResetTracking()
        {
            _isTrackingObject = false;
            _tracker?.Dispose();
            _tracker = null;
        }

        #endregion

        #region Detection Drawing and Database Operations

        /// <summary>
        /// Draws detection bounding box and label on the frame
        /// </summary>
        /// <param name="frame">Frame to draw on</param>
        /// <param name="detection">Detection result containing bounding box and label information</param>
        private void DrawDetection(Mat frame, YoloDotNet.Models.ObjectDetection detection)
        {
            var rect = detection.BoundingBox;
            int x = Math.Max(0, (int)rect.Left);
            int y = Math.Max(0, (int)rect.Top);
            int w = Math.Max(0, Math.Min(frame.Width - x, (int)(rect.Right - rect.Left)));
            int h = Math.Max(0, Math.Min(frame.Height - y, (int)(rect.Bottom - rect.Top)));

            if (w <= 0 || h <= 0) return;

            string label = detection.Label.Name;
            float conf = (float)detection.Confidence;

            // Draw bounding box
            Cv2.Rectangle(frame, new Rect(x, y, w, h), Scalar.Red, 2);

            // Draw label with confidence
            string text = $"{label} {conf:P1}";
            var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, 0.6, 1, out var baseline);

            int textY = y - 5;
            int textX = x;
            Point textOrg = new Point(textX, textY < textSize.Height ? y + baseline + 5 : textY);

            Cv2.PutText(frame, text, textOrg, HersheyFonts.HersheySimplex, 0.6, Scalar.LimeGreen, 2);
        }

        /// <summary>
        /// Writes detection information to the database
        /// Creates both media record and detection record with proper relationships
        /// </summary>
        /// <param name="detection">Detection result to save</param>
        /// <param name="frameBytes">JPEG bytes of the frame containing the detection</param>
        private async Task WriteDetectionToDatabase(YoloDotNet.Models.ObjectDetection detection, byte[] frameBytes)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ZootrackDbContext>();
                var detectionService = scope.ServiceProvider.GetRequiredService<IDetectionService>();

                // Save frame as media file
                var media = await SaveFrameAsMedia(frameBytes, context);

                // Create detection record
                var detectionRecord = new Detection
                {
                    DeviceId = 1, // Configure based on your device setup
                    MediaId = media.MediaId,
                    Confidence = (float)(detection.Confidence * 100),
                    DetectedAt = DateTime.Now,
                };

                await detectionService.CreateDetectionAsync(detectionRecord);

                _logger.LogInformation("Detection saved to database: {Label} with {Confidence}% confidence",
                    detection.Label.Name, detection.Confidence * 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write detection to database");
                // Don't throw - we don't want to break the video stream
            }
        }

        /// <summary>
        /// Saves frame bytes as a media file on disk and creates corresponding database record
        /// </summary>
        /// <param name="frameBytes">JPEG bytes of the frame</param>
        /// <param name="context">Database context for saving media record</param>
        /// <returns>Created Media entity</returns>
        private async Task<Media> SaveFrameAsMedia(byte[] frameBytes, ZootrackDbContext context)
        {
            try
            {
                // Create media directory
                string mediaDir = Path.Combine(AppContext.BaseDirectory, "MediaFiles", "Detections");
                Directory.CreateDirectory(mediaDir);

                // Generate unique filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string fileName = $"detection_{timestamp}.jpg";
                string filePath = Path.Combine(mediaDir, fileName);

                // Save frame to disk
                await File.WriteAllBytesAsync(filePath, frameBytes);

                // Create media record
                var media = new Media
                {
                    FilePath = Path.Combine("Detections", fileName), // Relative path
                    Type = "Image",
                    Timestamp = DateTime.Now,
                };

                context.Media.Add(media);
                await context.SaveChangesAsync();

                return media;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save frame as media");
                throw;
            }
        }

        #endregion

        #region Highlight Recording

        /// <summary>
        /// Handles highlight recording logic based on target detection
        /// Starts recording when targets are found, manages ongoing recording
        /// </summary>
        /// <param name="frame">Current frame to potentially record</param>
        /// <param name="targetFoundInFrame">Whether target animals were detected in this frame</param>
        private void HandleHighlightRecording(Mat frame, bool targetFoundInFrame)
        {
            if (_targetAnimals.Count == 0 || string.IsNullOrEmpty(_highlightSavePath))
            {
                return; // Recording not enabled or configured
            }

            if (targetFoundInFrame && !_isRecording)
            {
                StartHighlightRecording();
            }

            WriteFrameToRecording(frame);
        }

        /// <summary>
        /// Writes the current frame to the recording if recording is active
        /// </summary>
        /// <param name="frame">Frame to write to the recording</param>
        private void WriteFrameToRecording(Mat frame)
        {
            if (_isRecording && _writer != null && _writer.IsOpened())
            {
                try
                {
                    _writer.Write(frame);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing frame to highlight video.");
                    StopHighlightRecording();
                }
            }
        }

        /// <summary>
        /// Starts recording a new highlight video when target animals are detected
        /// Creates a unique filename and initializes video writer with proper codec settings
        /// </summary>
        private void StartHighlightRecording()
        {
            if (_isRecording || string.IsNullOrEmpty(_highlightSavePath))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_highlightSavePath);

                // Create unique filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                string fileName = $"highlight_{timestamp}.avi";
                string fullPath = Path.Combine(_highlightSavePath, fileName);

                // Initialize video writer
                int fourcc = VideoWriter.FourCC('M', 'J', 'P', 'G');
                _writer = new VideoWriter(fullPath, fourcc, _fps, new OpenCvSharp.Size(_width, _height));

                if (!_writer.IsOpened())
                {
                    _logger.LogError("Could not open VideoWriter for path {HighlightPath}", fullPath);
                    _writer?.Release();
                    _writer = null;
                    return;
                }

                _isRecording = true;
                _recordingStartTime = DateTime.UtcNow;
                _logger.LogInformation("Started recording highlight video to {HighlightPath}", fullPath);

                // Set timer to stop recording after duration
                SetupRecordingTimer();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start highlight recording.");
                CleanupRecordingResources();
            }
        }

        /// <summary>
        /// Sets up a timer to automatically stop recording after the highlight duration
        /// </summary>
        private void SetupRecordingTimer()
        {
            _recordingTimer?.Dispose();
            _recordingTimer = new Timer(_highlightDuration.TotalMilliseconds);
            _recordingTimer.Elapsed += (sender, e) => StopHighlightRecording();
            _recordingTimer.AutoReset = false;
            _recordingTimer.Start();
        }

        /// <summary>
        /// Stops the current highlight recording and releases associated resources
        /// Can be called by timer expiration or explicitly when needed
        /// </summary>
        private void StopHighlightRecording()
        {
            if (!_isRecording) return;

            _logger.LogInformation("Stopping highlight recording...");
            _isRecording = false;

            // Stop and dispose timer
            _recordingTimer?.Stop();
            _recordingTimer?.Dispose();
            _recordingTimer = null;

            // Release video writer
            try
            {
                _writer?.Release();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while releasing VideoWriter.");
            }
            finally
            {
                _writer = null;
            }

            _logger.LogInformation("Highlight recording stopped.");
        }

        /// <summary>
        /// Cleans up recording resources when recording fails to start
        /// </summary>
        private void CleanupRecordingResources()
        {
            _writer?.Release();
            _writer = null;
            _isRecording = false;
        }

        #endregion

        #region Processing Control

        /// <summary>
        /// Signals the service to start processing frames
        /// Must be called after successful initialization
        /// </summary>
        public void StartProcessing()
        {
            if (!IsInitialized)
            {
                _logger.LogError("Cannot start processing: Service not initialized.");
                return;
            }

            if (IsProcessing)
            {
                _logger.LogWarning("Processing already started.");
                return;
            }

            IsProcessing = true;
            _logger.LogInformation("Camera processing signaled to start.");
        }

        /// <summary>
        /// Signals the service to stop processing frames and stops any ongoing recording
        /// The actual stopping happens in the background service loop
        /// </summary>
        public void StopProcessing()
        {
            _logger.LogInformation("Stopping camera processing...");
            IsProcessing = false;

            if (_isRecording)
            {
                StopHighlightRecording();
            }

            _logger.LogInformation("Camera processing signaled to stop.");
        }

        #endregion

        #region Resource Management and Disposal

        /// <summary>
        /// Cleans up all service resources during disposal or initialization failure
        /// </summary>
        private void CleanupResources()
        {
            _capture?.Release();
            _capture = null;
            _yolo = null;
            IsInitialized = false;
            _isInitialized = false;
        }

        /// <summary>
        /// Protected dispose method implementing the dispose pattern
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.LogInformation("Disposing CameraService resources...");
                    StopProcessing();

                    _recordingTimer?.Stop();
                    _recordingTimer?.Dispose();

                    _capture?.Release();
                    _writer?.Release();
                    _yolo = null;

                    _tracker?.Dispose();
                    _tracker = null;

                    _logger.LogInformation("Managed resources disposed.");
                }

                _disposed = true;
                IsInitialized = false;
                _isInitialized = false;
                _logger.LogInformation("CameraService disposed.");
            }
        }

        /// <summary>
        /// Public dispose method for IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up if dispose is not called
        /// </summary>
        ~CameraService()
        {
            Dispose(false);
        }

        #endregion
    }
}
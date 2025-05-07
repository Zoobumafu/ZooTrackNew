// ZooTrack.WebAPI/Services/CameraService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging; // Added for logging
using OpenCvSharp;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using Timer = System.Timers.Timer; // Alias for clarity

namespace ZooTrack.Services
{
    public class FrameData
    {
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();
        public bool TargetDetected { get; set; } = false;
        public List<string> DetectedTargets { get; set; } = new List<string>();
    }

    public class CameraService : IDisposable
    {
        private readonly ILogger<CameraService> _logger;
        private VideoCapture? _capture;
        private Yolo? _yolo;
        private VideoWriter? _writer;
        private Timer? _recordingTimer; // Timer to stop recording

        private bool _isRecording = false;
        private DateTime _recordingStartTime;
        private readonly TimeSpan _highlightDuration = TimeSpan.FromSeconds(5);
        private string _highlightSavePath = string.Empty; // Path provided dynamically
        private List<string> _targetAnimals = new List<string>(); // Animals to trigger recording

        // Default resolution, might be overridden by camera capabilities
        private int _width = 640;
        private int _height = 480;
        private double _fps = 20.0; // Default FPS

        // Flag to prevent multiple simultaneous initializations
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();

        // Buffer for recent frames (optional, if pre-detection frames are needed)
        // For simplicity, we start recording *after* detection for 5s.

        // Tracker variables
        private TrackerMIL? _tracker;
        private Rect _trackedObjectBox;
        private bool _isTrackingObject = false;

        public bool IsInitialized { get; private set; } = false;
        public bool IsProcessing { get; private set; } = false;

        // Inject logger
        public CameraService(ILogger<CameraService> logger)
        {
            _logger = logger;
            // Defer initialization until StartProcessingAsync is called
        }

        public bool InitializeCameraAndYolo()
        {
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    _logger.LogWarning("CameraService already initialized.");
                    return true; // Already successfully initialized
                }

                try
                {
                    _logger.LogInformation("Initializing Camera and YOLO model...");

                    // Initialize camera (device 0)
                    _capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW); // Try DSHOW API for Windows compatibility
                    if (!_capture.Open(0, VideoCaptureAPIs.DSHOW))
                    {
                        _logger.LogError("Error: Could not open camera using DSHOW. Trying default API...");
                        _capture.Dispose(); // Dispose previous attempt
                        _capture = new VideoCapture(0); // Try default API
                        if (!_capture.Open(0))
                        {
                            _logger.LogError("Error: Could not open camera using default API.");
                            _capture = null;
                            return false;
                        }
                    }

                    // Set desired resolution (optional)
                    _capture.FrameWidth = _width;
                    _capture.FrameHeight = _height;

                    // Read actual resolution and FPS
                    _width = _capture.FrameWidth;
                    _height = _capture.FrameHeight;
                    _fps = _capture.Get(VideoCaptureProperties.Fps);
                    if (_fps <= 0 || double.IsNaN(_fps) || double.IsInfinity(_fps))
                    {
                        _logger.LogWarning("Could not get valid FPS from camera. Defaulting to {DefaultFps}", 20.0);
                        _fps = 20.0; // Use default if camera doesn't provide it
                    }

                    _logger.LogInformation("Camera opened successfully. Resolution: {Width}x{Height}, FPS: {Fps}", _width, _height, _fps);

                    // Load YOLOv10 ONNX model
                    string modelDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
                    string modelPath = Path.Combine(modelDirectory, "yolov10.onnx"); // Assuming yolov10.onnx

                    if (!Directory.Exists(modelDirectory))
                    {
                        _logger.LogError("Model directory not found at {ModelDirectory}", modelDirectory);
                        _capture?.Release();
                        return false;
                    }
                    if (!File.Exists(modelPath))
                    {
                        _logger.LogError("YOLO model not found at {ModelPath}", modelPath);
                        _capture?.Release();
                        return false;
                    }

                    _yolo = new Yolo(new YoloOptions
                    {
                        OnnxModel = modelPath,
                        ModelType = ModelType.ObjectDetection,
                        Cuda = false // Set true for GPU (ensure CUDA/cuDNN setup)
                    });

                    _logger.LogInformation("YOLO model loaded successfully.");
                    IsInitialized = true; // Mark as initialized
                    _isInitialized = true; // Set static flag

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal error during CameraService initialization.");
                    // Clean up resources
                    _capture?.Release();
                    _capture = null;
                    _yolo = null; // YoloDotNet might not need explicit disposal based on current code
                    IsInitialized = false;
                    _isInitialized = false;
                    return false; // Initialization failed
                }

                return IsInitialized; // Return success status
            }
        }

        public void SetProcessingTargets(List<string> targetAnimals, string savePath)
        {
            if (!IsInitialized)
            {
                _logger.LogWarning("Cannot set targets: Service not initialized.");
                return;
            }
            _targetAnimals = targetAnimals ?? new List<string>();
            _highlightSavePath = savePath;
            _logger.LogInformation("Processing targets set. Animals: {Animals}. Save Path: {Path}", string.Join(", ", _targetAnimals), _highlightSavePath);

            // Ensure the save directory exists
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
                    // Disable recording if directory fails? Or let StartRecording handle it.
                }
            }
        }

        private bool InitializeObjectTracker(Mat frame, YoloDotNet.Models.ObjectDetection initialDetection)
        {
            try
            {
                var rect = initialDetection.BoundingBox;
                _trackedObjectBox = new Rect(rect.Left, rect.Top, rect.Width, rect.Height); // Initialized as Rect
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

        // Called by the background service loop
        public FrameData? ProcessFrame()
        {
            if (!IsInitialized || _capture == null || !_capture.IsOpened() || _yolo == null)
            {
                _logger.LogWarning("ProcessFrame called but service is not ready.");
                return null;
            }

            using var frame = new Mat();
            try
            {
                if (!_capture.Read(frame) || frame.Empty())
                {
                    _logger.LogWarning("Could not read frame from camera.");
                    return null;
                }
            }
            catch (Exception readEx)
            {
                _logger.LogError(readEx, "Exception while reading frame from camera.");
                return null;
            }

            List<string> currentFrameDetections = new List<string>();
            bool targetFoundInFrame = false;
            YoloDotNet.Models.ObjectDetection? mainTarget = null; // Track the first target

            try
            {
                // 1. Run YOLO Detection
                Cv2.ImEncode(".jpg", frame, out byte[] rawData);
                using var skImage = SKImage.FromEncodedData(rawData);

                if (skImage == null)
                {
                    _logger.LogWarning("Could not decode frame using SkiaSharp.");
                    return new FrameData { JpegBytes = rawData, TargetDetected = false };
                }

                var results = _yolo.RunObjectDetection(skImage, confidence: 0.35f, iou: 0.6f);

                // 2. Process Detections & Draw Boxes
                foreach (var res in results)
                {
                    string label = res.Label.Name.ToLowerInvariant();
                    currentFrameDetections.Add(res.Label.Name);

                    if (_targetAnimals.Contains(label))
                    {
                        targetFoundInFrame = true;
                        if (mainTarget == null) // Track the first target found
                        {
                            mainTarget = res;
                        }
                    }

                    DrawDetection(frame, res);
                }

                // 3. Initialize Tracker if a target is found and not already tracking
                if (mainTarget != null && !_isTrackingObject)
                {
                    InitializeObjectTracker(frame, mainTarget);
                }

                // 4. Update Tracker if tracking
                if (_isTrackingObject && _tracker != null)
                {
                    Rect currentTrackedBox = _trackedObjectBox; // Create a local variable of type Rect
                    bool tracked = _tracker.Update(frame, ref currentTrackedBox); // Use 'ref' with Rect
                    _trackedObjectBox = currentTrackedBox; // Update the class member

                    if (tracked)
                    {
                        // Draw the tracked object's box
                        Cv2.Rectangle(frame, _trackedObjectBox, Scalar.Cyan, 2); // Draw using Rect
                    }
                    else
                    {
                        _logger.LogWarning("Tracking failed. Re-enabling YOLO detection.");
                        _isTrackingObject = false;
                        _tracker.Dispose();
                        _tracker = null;
                    }
                }
            }
            catch (Exception yoloEx)
            {
                _logger.LogError(yoloEx, "Error during YOLO detection or tracking.");
                Cv2.PutText(frame, "Detection Error", new Point(10, 30), HersheyFonts.HersheySimplex, 1.0, Scalar.Magenta, 2);
                _isTrackingObject = false; // Ensure tracking is off on error
                _tracker?.Dispose();
                _tracker = null;
            }

            // 5. Handle Highlight Recording Logic
            HandleHighlightRecording(frame, targetFoundInFrame);

            // 6. Encode Final Frame for Streaming
            Cv2.ImEncode(".jpg", frame, out byte[] finalJpegBytes);

            return new FrameData
            {
                JpegBytes = finalJpegBytes,
                TargetDetected = targetFoundInFrame,
                DetectedTargets = currentFrameDetections
            };
        }


        // previuos working function without tracking
        /*
        public FrameData? ProcessFrame()
        {
            if (!IsInitialized || _capture == null || !_capture.IsOpened() || _yolo == null)
            {
                _logger.LogWarning("ProcessFrame called but service is not ready.");
                // Consider returning a placeholder/error frame if needed for the stream
                // For now, returning null indicates an issue upstream.
                return null;
            }

            using var frame = new Mat();
            try
            {
                // Try reading a frame
                if (!_capture.Read(frame) || frame.Empty())
                {
                    _logger.LogWarning("Could not read frame from camera.");
                    // Maybe attempt to reopen camera? For now, return null.
                    return null;
                }
            }
            catch (Exception readEx)
            {
                _logger.LogError(readEx, "Exception while reading frame from camera.");
                return null; // Stop processing this frame
            }


            List<string> currentFrameDetections = new List<string>();
            bool targetFoundInFrame = false;

            // Use a clone for detection/drawing if the original is needed elsewhere
            // using var frameToProcess = frame.Clone(); // If needed

            try
            {
                // 1. Run YOLO Detection
                // Encode frame to byte array for SkiaSharp/YoloDotNet
                Cv2.ImEncode(".jpg", frame, out byte[] rawData);
                using var skImage = SKImage.FromEncodedData(rawData);

                if (skImage == null)
                {
                    _logger.LogWarning("Could not decode frame using SkiaSharp.");
                    // Return raw frame bytes without detection?
                    return new FrameData { JpegBytes = rawData, TargetDetected = false };
                }

                var results = _yolo.RunObjectDetection(skImage, confidence: 0.35f, iou: 0.6f); // Adjust confidence/IOU as needed

                // 2. Process Detections & Draw Boxes
                foreach (var res in results)
                {
                    string label = res.Label.Name.ToLowerInvariant(); // Use lower case for comparison
                    currentFrameDetections.Add(res.Label.Name); // Add original case name if needed elsewhere

                    // Check if the detected label is in our target list
                    if (_targetAnimals.Contains(label))
                    {
                        targetFoundInFrame = true;
                    }

                    // Draw bounding box (using code from your initial snippet)
                    DrawDetection(frame, res);
                }
            }
            catch (Exception yoloEx)
            {
                _logger.LogError(yoloEx, "Error during YOLO detection or drawing.");
                Cv2.PutText(frame, "Detection Error", new Point(10, 30), HersheyFonts.HersheySimplex, 1.0, Scalar.Magenta, 2);
            }

            // 3. Handle Highlight Recording Logic
            HandleHighlightRecording(frame, targetFoundInFrame);

            // 4. Encode Final Frame for Streaming
            Cv2.ImEncode(".jpg", frame, out byte[] finalJpegBytes);

            return new FrameData
            {
                JpegBytes = finalJpegBytes,
                TargetDetected = targetFoundInFrame,
                DetectedTargets = currentFrameDetections // Send list of detected animals
            };
        }
        */

        private void DrawDetection(Mat frame, YoloDotNet.Models.ObjectDetection res)
        {
            var rect = res.BoundingBox;
            int x = Math.Max(0, (int)rect.Left);
            int y = Math.Max(0, (int)rect.Top);
            // Ensure width/height calculation doesn't result in negative values if rect goes outside bounds slightly
            int w = Math.Max(0, Math.Min(frame.Width - x, (int)(rect.Right - rect.Left)));
            int h = Math.Max(0, Math.Min(frame.Height - y, (int)(rect.Bottom - rect.Top)));

            if (w <= 0 || h <= 0) return; // Skip invalid boxes

            string label = res.Label.Name;
            float conf = (float)res.Confidence;

            Cv2.Rectangle(frame, new Rect(x, y, w, h), Scalar.Red, 2);
            string text = $"{label} {conf:P1}";
            var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, 0.6, 1, out var baseline); // Thinner text thickness

            // Simple text placement above the box
            int textY = y - 5;
            int textX = x;
            Point textOrg = new Point(textX, textY < textSize.Height ? y + baseline + 5 : textY); // Adjust if too close to top

            // Background rectangle for text
            // Cv2.Rectangle(frame, new Rect(textOrg.X - 2, textOrg.Y - textSize.Height - baseline, textSize.Width + 4, textSize.Height + baseline + 4), Scalar.LimeGreen, -1);
            Cv2.PutText(frame, text, textOrg, HersheyFonts.HersheySimplex, 0.6, Scalar.LimeGreen, 2); // White text might be more visible
        }


        private void HandleHighlightRecording(Mat frame, bool targetFoundInFrame)
        {
            if (_targetAnimals.Count == 0 || string.IsNullOrEmpty(_highlightSavePath))
            {
                // Recording not enabled or configured
                return;
            }

            if (targetFoundInFrame && !_isRecording)
            {
                // Start recording a new highlight
                StartHighlightRecording();
            }

            // If currently recording, write the frame
            if (_isRecording && _writer != null && _writer.IsOpened())
            {
                try
                {
                    _writer.Write(frame);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing frame to highlight video.");
                    // Optionally stop recording on write error?
                    StopHighlightRecording();
                }
            }
        }

        private void StartHighlightRecording()
        {
            if (_isRecording) return; // Already recording

            if (string.IsNullOrEmpty(_highlightSavePath))
            {
                _logger.LogWarning("Cannot start recording: Highlight save path not set.");
                return;
            }

            try
            {
                // Ensure directory exists (double check)
                Directory.CreateDirectory(_highlightSavePath);

                // Create unique filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                string fileName = $"highlight_{timestamp}.avi";
                string fullPath = Path.Combine(_highlightSavePath, fileName);

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

                // Start timer to stop recording after duration
                _recordingTimer?.Dispose(); // Dispose previous timer if any
                _recordingTimer = new Timer(_highlightDuration.TotalMilliseconds);
                _recordingTimer.Elapsed += (sender, e) => StopHighlightRecording();
                _recordingTimer.AutoReset = false; // Only fire once
                _recordingTimer.Start();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start highlight recording.");
                _writer?.Release(); // Clean up if partially opened
                _writer = null;
                _isRecording = false;
            }
        }

        private void StopHighlightRecording()
        {
            // This can be called by the timer or explicitly
            if (!_isRecording) return;

            _logger.LogInformation("Stopping highlight recording...");
            _isRecording = false;

            _recordingTimer?.Stop();
            _recordingTimer?.Dispose();
            _recordingTimer = null;

            // Use a small delay before releasing writer to ensure last frames are potentially written? (Maybe not needed)
            // Task.Delay(100).Wait(); // Avoid Wait in async context if possible

            try
            {
                _writer?.Release(); // Safely release the writer
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while releasing VideoWriter.");
            }
            finally
            {
                _writer = null; // Set to null after attempting release
            }

            _logger.LogInformation("Highlight recording stopped.");
        }

        public void StopProcessing()
        {
            _logger.LogInformation("Stopping camera processing...");
            IsProcessing = false; // Signal background service to stop loop
            // Note: Actual stopping happens in the background service loop check

            // If recording is happening when processing stops, stop it.
            if (_isRecording)
            {
                StopHighlightRecording();
            }
            _logger.LogInformation("Camera processing signaled to stop.");
        }

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
            // Note: Actual starting happens in the background service
        }


        // Dispose Pattern
        private bool _disposed = false;
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

                    _tracker?.Dispose(); // Dispose the tracker
                    _tracker = null;

                    _logger.LogInformation("Managed resources disposed.");
                }

                _disposed = true;
                IsInitialized = false;
                _isInitialized = false;
                _logger.LogInformation("CameraService disposed.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CameraService()
        {
            Dispose(false);
        }
    }
}
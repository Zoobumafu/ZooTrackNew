using ZooTrack.Data;
using ZooTrack.Models;
using Microsoft.EntityFrameworkCore;
using ZooTrackBackend.Services;
using OpenCvSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace ZooTrack.Services
{
    /// <summary>
    /// This service extracts frames from media files during detection events
    /// and correlating detections to track the same objects across multiple frames.
    /// To determine if an object is already tracked or new one, it calculates IoU on the object.
    /// Low IoU rate initiates creation of new Tracker instance.
    /// Old Trackers get deleted if not used recently.
    /// In case of multiple animals from same species, each will get its own Tracker instance.
    /// In addition, the service extract frames, saves them and log it.
    /// </summary>

    public class DetectionMediaService : IDisposable
    {
        #region Constants and Configuration

        /// Number of frames to extract per second for tracking analysis
        private const int FRAMES_PER_SECOND = 5;
        /// Number of seconds before the detection moment to include in frame extraction
        private const int SECONDS_BEFORE_DETECTION = 10;
        /// Number of seconds after the detection moment to include in frame extraction
        private const int SECONDS_AFTER_DETECTION = 20;
        /// Maximum tracking age in seconds before a tracker is considered stale
        private const int MAX_TRACKING_AGE_SECONDS = 30;
        /// Minimum overlap threshold for associating detections with existing trackers
        private const double MIN_OVERLAP_THRESHOLD = 0.3;

        #endregion

        #region Fields

        private readonly ZootrackDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogService _logService;
        private readonly Dictionary<int, string> _trackerObjects = new Dictionary<int, string>();

        // containing Tracking Instances
        private readonly Dictionary<int, MilTrackerService> _activeTrackers = new Dictionary<int, MilTrackerService>();
        private readonly Dictionary<int, DateTime> _lastSeenTimes = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, Rect> _lastKnownBounds = new Dictionary<int, Rect>();
        private bool _disposed = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DetectionMediaService class.
        /// </summary>
        /// <param name="context">Database context for accessing detection and media data</param>
        /// <param name="environment">Web host environment for accessing file paths</param>
        /// <param name="logService">Service for logging operations and errors</param>
        public DetectionMediaService(ZootrackDbContext context, IWebHostEnvironment environment, ILogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Extracts frames from media associated with a detection event and performs object correlation.
        /// This is the main entry point for frame extraction operations.
        /// </summary>
        /// <param name="detection">The detection event for which to extract frames</param>
        /// <returns>A task representing the asynchronous frame extraction operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when detection parameter is null</exception>
        /// <exception cref="Exception">Thrown when media file is not found or extraction fails</exception>
        public async Task ExtractFramesAsync(Detection detection)
        {
            if (detection == null)
                throw new ArgumentNullException(nameof(detection));

            try
            {
                await _logService.AddLogAsync(1, "FrameExtractionStarted",
                    $"Starting smart frame extraction for detection {detection.DetectionId}", "Info", detection.DetectionId);

                CleanupStaleTrackers(); // Clean up stale trackers

                // Validate and load media file
                var media = await LoadAndValidateMediaAsync(detection.MediaId);
                var mediaPath = GetMediaPath(media.FilePath);

                var outputDir = CreateOutputDirectory(detection.DetectionId);

                // Extract frames for tracking analysis
                await ExtractTrackingFramesAsync(mediaPath, outputDir, detection);

                // Correlate with existing detections to identify same objects
                await InitializeOrUpdateTracking(detection, mediaPath);

                // Save all changes to database
                await _context.SaveChangesAsync();

                await _logService.AddLogAsync(1, "FrameExtractionCompleted",
                    $"Smart frame extraction completed for detection {detection.DetectionId}", "Info", detection.DetectionId);
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "FrameExtractionFailed",
                    $"Frame extraction failed for detection {detection.DetectionId}: {ex.Message}", "Error", detection.DetectionId);
                throw;
            }
        }

        #endregion

        #region Private Methods - Media Validation and Path Management

        /// <summary>
        /// Loads and validates that the media file exists in the database and on disk.
        /// </summary>
        /// <param name="mediaId">The ID of the media to load</param>
        /// <returns>The loaded media entity</returns>
        /// <exception cref="Exception">Thrown when media is not found in database</exception>
        private async Task<Media> LoadAndValidateMediaAsync(int mediaId)
        {
            var media = await _context.Media.FindAsync(mediaId);
            if (media == null)
            {
                throw new Exception($"Media with ID {mediaId} not found.");
            }
            return media;
        }

        /// <summary>
        /// Constructs the full file path for a media file and validates its existence.
        /// </summary>
        /// <param name="filePath">The relative file path from the media record</param>
        /// <returns>The full path to the media file</returns>
        /// <exception cref="Exception">Thrown when the media file does not exist on disk</exception>
        private string GetMediaPath(string filePath)
        {
            var mediaPath = Path.Combine(_environment.ContentRootPath, "MediaFiles", filePath);
            if (!File.Exists(mediaPath))
            {
                throw new Exception($"Media file not found: {mediaPath}");
            }
            return mediaPath;
        }

        /// <summary>
        /// Creates the output directory for storing extracted frames from a detection.
        /// </summary>
        /// <param name="detectionId">The ID of the detection for which to create the directory</param>
        /// <returns>The path to the created output directory</returns>
        private string CreateOutputDirectory(int detectionId)
        {
            var outputDir = Path.Combine(_environment.ContentRootPath, "SavedDetections", detectionId.ToString());
            Directory.CreateDirectory(outputDir);
            return outputDir;
        }
        #endregion

        #region Private Methods - Frame Extraction

        private async Task ExtractTrackingFramesAsync(string videoPath, string outputDir, Detection detection)
        {
            try
            {
                // Calculate the time window
                var timeWindow = CalculateExtractionTimeWindow(detection.DetectedAt);
                var totalFramesToExtract = (int)(timeWindow.Duration * FRAMES_PER_SECOND);
                var frameInterval = 1.0 / FRAMES_PER_SECOND;
                /*
                await _logService.AddLogAsync(1, "FrameExtractionPlan",
                    $"Extracting {totalFramesToExtract} frames at {FRAMES_PER_SECOND} FPS for detection {detection.DetectionId}",
                    "Info", detection.DetectionId);
                */
                // Extract frames at regular intervals using your constants
                await ExtractRegularFrames(outputDir, timeWindow, totalFramesToExtract, frameInterval);

                // Extract and save key frames
                await SaveKeyFrames(outputDir, detection);

                await _logService.AddLogAsync(1, "FramesExtracted",
                    $"Extracted {totalFramesToExtract} tracking frames for detection {detection.DetectionId}",
                    "Info", detection.DetectionId);
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "FrameExtractionError",
                    $"Error extracting frames: {ex.Message}", "Error", detection.DetectionId);
                throw;
            }
        }


        // calculate extraction time window based on the constants
        private (DateTime StartTime, DateTime EndTime, double Duration) CalculateExtractionTimeWindow(DateTime detectionTime)
        {
            var startTime = detectionTime.AddSeconds(-SECONDS_BEFORE_DETECTION);
            var endTime = detectionTime.AddSeconds(SECONDS_AFTER_DETECTION);
            var duration = (endTime - startTime).TotalSeconds;

            return (startTime, endTime, duration);
        }

        private async Task ExtractRegularFrames(string outputDir, (DateTime StartTime, DateTime EndTime, double Duration) timeWindow, int totalFrames, double frameInterval)
        {
            // Get the media/video file that contains this time window
            var media = await FindMediaForTimeWindow(timeWindow);
            if (media == null)
            {
                await _logService.AddLogAsync(1, "MediaNotFound",
                    $"No media found for time window {timeWindow.StartTime} - {timeWindow.EndTime}", "Warning");
                return;
            }

            string videoPath = GetMediaFilePath(media);
            if (!File.Exists(videoPath))
            {
                await _logService.AddLogAsync(1, "VideoFileNotFound",
                    $"Video file not found: {videoPath}", "Error", media.MediaId);
                return;
            }

            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
            {
                await _logService.AddLogAsync(1, "VideoOpenError",
                    $"Failed to open video file: {videoPath}", "Error", media.MediaId);
                return;
            }

            double fps = capture.Fps > 0 ? capture.Fps : 20.0; // Fallback like your CameraService
            DateTime videoStartTime = media.Timestamp; // Using Media.Timestamp as the video start time

            /*
            await _logService.AddLogAsync(1, "FrameExtractionStarted",
                $"Starting extraction of {totalFrames} frames from {media.FilePath} at {FRAMES_PER_SECOND} FPS",
                "Info", media.MediaId);
            */

            for (int i = 0; i < totalFrames; i++)
            {
                var frameTime = timeWindow.StartTime.AddSeconds(i * frameInterval);
                var framePath = Path.Combine(outputDir, $"frame_{i:000}.jpg");

                double secondsFromVideoStart = (frameTime - videoStartTime).TotalSeconds;
                if (secondsFromVideoStart < 0)
                {
                    await _logService.AddLogAsync(1, "FrameSkipped",
                        $"Skipping frame {i} - time {frameTime} is before video start", "Warning", media.MediaId);
                    continue;
                }

                int targetFrameNumber = (int)(secondsFromVideoStart * fps);

                // setting current position of the video stream to a specific frame number
                capture.PosFrames = targetFrameNumber;
                using var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty())
                    continue;

                // save to file
                DrawTrackersOnFrame(frame, frameTime);
                var encodingParams = new int[] { (int)ImwriteFlags.JpegQuality, 90 };
                Cv2.ImEncode(".jpg", frame, out byte[] jpegBytes, encodingParams);
                await SaveFrameToFile(jpegBytes, framePath, frameTime);

                // update all active trackers
                UpdateActiveTrackers(frame, frameTime, media.MediaId);

                // save to DB only in batches of 10
                if (i % 10 == 0)
                    await _context.SaveChangesAsync();
            }

        }


        // saving frames at the detection event, then before and after it for better tracking
        private async Task SaveKeyFrames(string outputDir, Detection detection)
        {
            // Load the associated media for this detection
            var media = await LoadAndValidateMediaAsync(detection.MediaId);
            string videoPath = GetMediaFilePath(media);

            if (!File.Exists(videoPath))
            {
                await _logService.AddLogAsync(1, "VideoFileNotFound",
                    $"Video file not found for key frame extraction: {videoPath}", "Error", detection.DetectionId);
                return;
            }

            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
            {
                await _logService.AddLogAsync(1, "VideoOpenError",
                    $"Failed to open video file for key frames: {videoPath}", "Error", detection.DetectionId);
                return;
            }

            double fps = capture.Fps > 0 ? capture.Fps : 20.0;
            DateTime videoStartTime = media.Timestamp;

            // Use your existing constants for key frame timing
            var keyFrames = new[]
            {
                ("detection_moment.jpg", detection.DetectedAt),
                ("before_detection.jpg", detection.DetectedAt.AddSeconds(-5)), // Keep your existing 5-second offset
                ("after_detection.jpg", detection.DetectedAt.AddSeconds(5))
            };

            await _logService.AddLogAsync(1, "KeyFrameExtractionStarted",
                $"Extracting key frames for detection {detection.DetectionId}", "Info", detection.DetectionId);

            foreach (var (fileName, frameTime) in keyFrames)
            {
                var keyFramePath = Path.Combine(outputDir, fileName);

                double secondsFromVideoStart = (frameTime - videoStartTime).TotalSeconds;
                if (secondsFromVideoStart < 0)
                {
                    await _logService.AddLogAsync(1, "KeyFrameSkipped",
                        $"Skipping key frame {fileName} - time {frameTime} is before video start", "Warning", detection.DetectionId);
                    continue;
                }

                int targetFrameNumber = (int)(secondsFromVideoStart * fps);

                byte[] frameData = await ExtractFrameAtPosition(capture, targetFrameNumber, frameTime, detection.DetectionId);

                if (frameData != null)
                {
                    await SaveFrameToFile(frameData, keyFramePath, frameTime);
                }
            }
        }

        // extract position frame, returns frame as byte[]
        // Helper method to private async Task SaveKeyFrames(string outputDir, Detection detection)
        private async Task<byte[]> ExtractFrameAtPosition(VideoCapture capture, int frameNumber, DateTime frameTime, int? contextId = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Seek to the specific frame
                    capture.PosFrames = frameNumber;
                    using var frame = new Mat();
                    bool readSuccess = capture.Read(frame);

                    if (!readSuccess || frame.Empty())
                    {
                        throw new InvalidOperationException($"Failed to read frame {frameNumber} at {frameTime}");
                    }

                    // Use the same JPEG encoding approach as your existing system
                    var encodingParams = new int[] { (int)ImwriteFlags.JpegQuality, 90 };
                    bool encodeSuccess = Cv2.ImEncode(".jpg", frame, out byte[] jpegBytes, encodingParams);

                    if (!encodeSuccess)
                    {
                        throw new InvalidOperationException($"Failed to encode frame {frameNumber} at {frameTime}");
                    }

                    return jpegBytes;
                }
                catch (Exception ex)
                {
                    // Log in background, don't wait
                    _ = Task.Run(async () => await _logService.AddLogAsync(1, "FrameExtractionError",
                        $"Error extracting frame {frameNumber} at {frameTime}: {ex.Message}", "Error", contextId));
                    throw;
                }
            });
        }

        
        private async Task SaveFrameToFile(byte[] frameData, string framePath, DateTime frameTime)
        {
            if (frameData == null || frameData.Length == 0)
            {
                await _logService.AddLogAsync(1, "SaveFrameFailed",
                    $"Attempted to save empty frame data at {frameTime}", "Warning");
                return;
            }

            try
            {
                // Ensure the directory exists 
                string directory = Path.GetDirectoryName(framePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(framePath, frameData);
                await _logService.AddLogAsync(1, "FrameSaved",
                    $"Frame saved to {framePath} at {frameTime}", "Info");
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "SaveFrameError",
                    $"Error saving frame to {framePath} at {frameTime}: {ex.Message}", "Error");
                throw;
            }
        }

        // Helper method to find media containing a specific time window
        private async Task<Media> FindMediaForTimeWindow((DateTime StartTime, DateTime EndTime, double Duration) timeWindow)
        {
            // Query your database to find media that contains the time window
            // Since Media doesn't have Duration, we'll find the media with Timestamp closest to but before the StartTime
            var media = await _context.Media
                .Where(m => m.Timestamp <= timeWindow.StartTime && m.Type == "video")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();

            return media;
        }

        // Helper method to get the full file path for a media record
        private string GetMediaFilePath(Media media)
        {
            // Use the FilePath directly from the Media record
            if (Path.IsPathRooted(media.FilePath))
            {
                return media.FilePath; // Already a full path
            }
            else
            {
                // If it's a relative path, combine with ContentRootPath
                return Path.Combine(_environment.ContentRootPath, media.FilePath);
            }
        }

        #endregion

        #region Private Methods - TRACKING
        private void CleanupStaleTrackers()
        {
            var now = DateTime.UtcNow;
            var staleIds = _lastSeenTimes
                .Where(kv => (now - kv.Value).TotalSeconds > MAX_TRACKING_AGE_SECONDS)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var id in staleIds)
            {
                _activeTrackers[id].Dispose();
                _activeTrackers.Remove(id);
                _lastSeenTimes.Remove(id);
                _lastKnownBounds.Remove(id);
                _trackerObjects.Remove(id);
            }
        }

        // detection event: determine if is part of already tracked detection or of new one.
        // determine by calling IoU calculation
        private async Task InitializeOrUpdateTracking(Detection detection, string videoPath)
        {
            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened()) return;

            // go to the relevant frame by: detection.DetectedAt
            double fps = capture.Fps > 0 ? capture.Fps : 20.0;
            double secondsFromStart = (detection.DetectedAt - detection.Media.Timestamp).TotalSeconds;
            int frameNumber = (int)(secondsFromStart * fps);

            capture.PosFrames = frameNumber;
            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty()) return;

            var newBox = new Rect(
                (int)detection.BoundingBoxX,
                (int)detection.BoundingBoxY,
                (int)detection.BoundingBoxWidth,
                (int)detection.BoundingBoxHeight
            );

            // search for exist trackers with overlaps
            int? matchedId = FindMatchingTracker(newBox); // call for IoU calculation
            if (matchedId.HasValue)
            {
                // update exists tracker
                _lastSeenTimes[matchedId.Value] = DateTime.UtcNow;
                _lastKnownBounds[matchedId.Value] = newBox;
                detection.TrackingId = matchedId.Value;
            }
            else
            {
                // create new tracker 
                int newId = await GetNextTrackingId();
                var tracker = new MilTrackerService();
                if (tracker.Initialize(frame, newBox))
                {
                    _activeTrackers[newId] = tracker;
                    _lastSeenTimes[newId] = DateTime.UtcNow;
                    _lastKnownBounds[newId] = newBox;
                    _trackerObjects[newId] = detection.DetectedObject ?? "Unknown";
                    detection.TrackingId = newId;

                    await _logService.AddLogAsync(1, "TrackerCreated",
                          $"Created tracker {newId} for detection {detection.DetectionId} at {detection.DetectedAt}",
                          "Info", detection.DetectionId);
                }
                else
                {
                    await _logService.AddLogAsync(1, "TrackerInitFailed",
                        $"Failed to initialize tracker for detection {detection.DetectionId}",
                        "Error", detection.DetectionId);
                }

            }
            try
            {
                _context.Detections.Update(detection);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    1,
                    "DbUpdateFailed",
                    $"Failed to save tracking update for detection {detection.DetectionId}: {ex.Message}",
                    "Error",
                    detection.DetectionId
                );
            }
        }

        private int? FindMatchingTracker(Rect newBox)
        {
            foreach (var kvp in _lastKnownBounds)
            {
                double overlap = CalculateIoU(newBox, kvp.Value); // call for IoU calculation
                if (overlap >= MIN_OVERLAP_THRESHOLD)
                    return kvp.Key;
            }
            return null;
        }

        // Intersection over Union
        private double CalculateIoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.Left, b.Left);
            int y1 = Math.Max(a.Top, b.Top);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);

            int intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            int union = a.Width * a.Height + b.Width * b.Height - intersection;

            return union > 0 ? (double)intersection / union : 0;
        }

        // manage the active trackers, saves new locations or stops tracking
        private void UpdateActiveTrackers(Mat frame, DateTime frameTime, int mediaId)
        {
            foreach (var kvp in _activeTrackers.ToList()) // work on temp list
            {
                var tracker = kvp.Value;
                var id = kvp.Key;

                var newBox = tracker.Update(frame); //call for TrackerMIL algorithm
                if (newBox.HasValue)
                {
                    // update status
                    _lastKnownBounds[id] = newBox.Value;
                    _lastSeenTimes[id] = DateTime.UtcNow;

                    // create new detection
                    var trackingDetection = new Detection
                    {
                        MediaId = mediaId,
                        TrackingId = id,
                        DetectedAt = frameTime,
                        BoundingBoxX = newBox.Value.X,
                        BoundingBoxY = newBox.Value.Y,
                        BoundingBoxWidth = newBox.Value.Width,
                        BoundingBoxHeight = newBox.Value.Height,
                        Confidence = 1.0f, // default value
                        DetectedObject = _trackerObjects.ContainsKey(id) ? _trackerObjects[id] : "Unknown"
                    };

                    _context.Detections.Add(trackingDetection);
                }
                else
                {
                    tracker.Dispose();
                    _activeTrackers.Remove(id);
                    _lastSeenTimes.Remove(id);
                    _lastKnownBounds.Remove(id);
                    _trackerObjects.Remove(id);

                    _logService.AddLogAsync(1, "TrackingLost",
                        $"Tracker {id} removed at {frameTime}", "Warning");
                }
            }
        }

        private void DrawTrackersOnFrame(Mat frame, DateTime frameTime)
        {
            foreach (var kvp in _lastKnownBounds)
            {
                int id = kvp.Key;
                Rect box = kvp.Value;

                string label = _trackerObjects.ContainsKey(id) ? _trackerObjects[id] : "Unknown";
                Cv2.Rectangle(frame, box, Scalar.Red, 2);

                string text = $"ID:{id} {label}";
                Cv2.PutText(frame, text,
                    new Point(box.X, Math.Max(0, box.Y - 10)),
                    HersheyFonts.HersheySimplex,
                    0.6, Scalar.Blue, 2);
            }
        }


        /// <summary>
        /// Generates the next available tracking ID by finding the maximum existing tracking ID and incrementing it.
        /// </summary>
        /// <returns>The next available tracking ID</returns>
        private async Task<int> GetNextTrackingId()
        {
            var maxTrackingId = await _context.Detections
                .Where(d => d.TrackingId.HasValue)
                .MaxAsync(d => (int?)d.TrackingId) ?? 0;

            return maxTrackingId + 1;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var tracker in _activeTrackers.Values)
                    tracker.Dispose();
                _activeTrackers.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}
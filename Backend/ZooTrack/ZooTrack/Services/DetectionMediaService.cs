using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrackBackend.Services;

namespace ZooTrack.Services
{
    public class DetectionMediaService : IDisposable
    {
        #region Constants and Configuration

        private const int FRAMES_PER_SECOND = 5;
        private const int SECONDS_BEFORE_DETECTION = 10;
        private const int SECONDS_AFTER_DETECTION = 20;
        private const int MAX_TRACKING_AGE_SECONDS = 30;
        private const double MIN_OVERLAP_THRESHOLD = 0.3;

        private DateTime _lastStatusLog = DateTime.MinValue;
        private const int STATUS_LOG_INTERVAL_SECONDS = 30;
        private int _batchCounter = 0;

        #endregion

        #region Fields

        private readonly ZootrackDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogService _logService;
        private readonly Dictionary<int, string> _trackerObjects = new Dictionary<int, string>();

        private readonly Dictionary<int, MilTrackerService> _activeTrackers = new Dictionary<int, MilTrackerService>();
        private readonly Dictionary<int, DateTime> _lastSeenTimes = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, Rect> _lastKnownBounds = new Dictionary<int, Rect>();
        private bool _disposed = false;

        #endregion

        #region Constructor

        public DetectionMediaService(ZootrackDbContext context, IWebHostEnvironment environment, ILogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }
        #endregion

        #region Public Methods

        public async Task ExtractFramesAsync(Detection detection)
        {
            if (detection == null)
                throw new ArgumentNullException(nameof(detection));
            if (!detection.MediaId.HasValue)
                throw new InvalidOperationException($"Detection {detection.DetectionId} has no associated media");

            try
            {
                CleanupStaleTrackers();

                var media = await LoadAndValidateMediaAsync(detection.MediaId.Value);
                var mediaPath = GetMediaPath(media.FilePath);
                var outputDir = CreateOutputDirectory(detection.DetectionId);

                await ExtractTrackingFramesAsync(mediaPath, outputDir, detection);

                await InitializeOrUpdateTrackingForSaving(detection);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var detectionIdForLog = detection.DetectionId > 0 ? detection.DetectionId : (int?)null;
                await _logService.AddLogAsync(1, "FrameExtractionFailed",
                    $"Frame extraction failed: {ex.Message}", "Error", detectionIdForLog);
                throw;
            }
        }

        // ⭐ FIXED: Don't pass detectionId to logs until detection is saved
        public async Task ProcessDetectionForSaving(Detection detection, byte[] frameBytes)
        {
            if (detection == null)
                throw new ArgumentNullException(nameof(detection));

            //  NULL detectionId - detection doesn't exist yet
            await _logService.AddLogAsync(1, "ProcessingDetection",
                $"Starting to process detection: {detection.DetectedObject} at {detection.DetectedAt}",
                "Debug", null);

            try
            {
                await _logService.AddLogAsync(1, "Step1_CleanupTrackers",
                    "Step 1: Cleaning up stale trackers", "Debug", null);

                CleanupStaleTrackers();

                await _logService.AddLogAsync(1, "Step2_CheckShouldSave",
                    $"Step 2: Checking if detection should be saved (Device: {detection.DeviceId}, Object: {detection.DetectedObject})",
                    "Debug", null);

                if (!await ShouldSaveDetection(detection))
                {
                    await _logService.AddLogAsync(1, "DetectionSkipped",
                        $"Skipped - similar detection recently saved: {detection.DetectedObject}",
                        "Info", null);
                    return;
                }

                await _logService.AddLogAsync(1, "Step3_StartSaveFrame",
                    $" Passed ShouldSave check! Step 3: About to save frame (Device: {detection.DeviceId}, FrameSize: {frameBytes?.Length ?? 0} bytes)",
                    "Info", null);

                int mediaId = await SaveDetectionFrameAsync(frameBytes, detection.DeviceId);

                await _logService.AddLogAsync(1, "Step3_FrameSavedSuccess",
                    $" Step 3 COMPLETE: Frame saved successfully! MediaId={mediaId}",
                    "Info", null);

                detection.MediaId = mediaId;

                await _logService.AddLogAsync(1, "Step3_5_EnsureEventId",
                    $"Step 3.5: Ensuring Detection has valid EventId",
                    "Debug", null);

                if (!detection.EventId.HasValue || detection.EventId.Value <= 0)
                {
                    var activeEvent = await GetOrCreateActiveEventAsync();
                    detection.EventId = activeEvent.EventId;

                    await _logService.AddLogAsync(1, "Step3_5_EventIdSet",
                        $" EventId set to {detection.EventId} (Active Event)",
                        "Info", null);
                }

                await _logService.AddLogAsync(1, "Step4_StartTracking",
                    $"Step 4: About to initialize/update tracking (MediaId: {mediaId}, EventId: {detection.EventId})",
                    "Debug", null);

                // ⭐ This method now saves the detection and returns the ID
                await InitializeOrUpdateTrackingForSaving(detection);

                //  NOW detection.DetectionId exists, safe to log with it
                await _logService.AddLogAsync(1, "Step4_TrackingComplete",
                    $" Step 4 COMPLETE: Detection saved to database! DetectionId={detection.DetectionId}, TrackingId={detection.TrackingId}",
                    "Info", detection.DetectionId);

                await LogStatusIfNeeded();

                //  NOW detection.DetectionId exists, safe to log with it
                await _logService.AddLogAsync(1, "ProcessDetectionSuccess",
                    $"FULLY COMPLETED: Detection {detection.DetectionId} for '{detection.DetectedObject}' saved successfully with {detection.Confidence:F1}% confidence!",
                    "Info", detection.DetectionId);
            }
            catch (Exception ex)
            {
                //  NULL detectionId - detection might not exist yet
                await _logService.AddLogAsync(1, "ProcessDetectionError",
                    $"CRITICAL ERROR in ProcessDetectionForSaving:\n" +
                    $"Exception Type: {ex.GetType().Name}\n" +
                    $"Message: {ex.Message}\n" +
                    $"Stack Trace: {ex.StackTrace}\n" +
                    $"Inner Exception: {ex.InnerException?.Message ?? "None"}\n" +
                    $"Detection Object: {detection.DetectedObject}\n" +
                    $"Device ID: {detection.DeviceId}\n" +
                    $"Media ID: {detection.MediaId}\n" +
                    $"Event ID: {detection.EventId}",
                    "Error", null);
                throw;
            }
        }

        public async Task LogTrackingStatusSummaryAsync()
        {
            if (_activeTrackers.Count == 0)
            {
                await _logService.AddLogAsync(1, "TrackingStatus",
                    "No active trackers", "Info", null);
                return;
            }

            var activeTrackerCount = _activeTrackers.Count;
            var trackedObjects = string.Join(", ", _trackerObjects.Values.Distinct());
            var oldestTracker = _lastSeenTimes.Values.Min();
            var trackingDuration = (DateTime.UtcNow - oldestTracker).TotalSeconds;

            await _logService.AddLogAsync(1, "TrackingStatus",
                $"Active trackers: {activeTrackerCount}, Objects: [{trackedObjects}], Longest tracking: {trackingDuration:F0}s",
                "Info", null);
        }

        #endregion

        #region Private Methods - Event Management

        private async Task<Event> GetOrCreateActiveEventAsync()
        {
            var activeEvent = await _context.Events
                .Where(e => e.Status == "Active" || e.EndTime > DateTime.Now)
                .OrderByDescending(e => e.StartTime)
                .FirstOrDefaultAsync();

            if (activeEvent != null)
            {
                await _logService.AddLogAsync(1, "EventFound",
                    $"Found existing active event: EventId={activeEvent.EventId}",
                    "Debug", null);
                return activeEvent;
            }

            var newEvent = new Event
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(4),
                Status = "Active"
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            await _logService.AddLogAsync(1, "EventCreated",
                $"Created new active event: EventId={newEvent.EventId} (StartTime: {newEvent.StartTime}, EndTime: {newEvent.EndTime})",
                "Info", null);

            return newEvent;
        }

        #endregion

        #region Private Methods - Media Validation and Path Management

        private async Task<Media> LoadAndValidateMediaAsync(int mediaId)
        {
            var media = await _context.Media.FindAsync(mediaId);
            if (media == null)
            {
                throw new Exception($"Media with ID {mediaId} not found.");
            }
            return media;
        }

        private string GetMediaPath(string filePath)
        {
            var mediaPath = Path.Combine(_environment.ContentRootPath, "MediaFiles", filePath);
            if (!File.Exists(mediaPath))
            {
                throw new Exception($"Media file not found: {mediaPath}");
            }
            return mediaPath;
        }

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
                var timeWindow = CalculateExtractionTimeWindow(detection.DetectedAt);
                var totalFramesToExtract = (int)(timeWindow.Duration * FRAMES_PER_SECOND);
                var frameInterval = 1.0 / FRAMES_PER_SECOND;

                await ExtractRegularFrames(outputDir, timeWindow, totalFramesToExtract, frameInterval);

                await SaveKeyFrames(outputDir, detection);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private (DateTime StartTime, DateTime EndTime, double Duration) CalculateExtractionTimeWindow(DateTime detectionTime)
        {
            var startTime = detectionTime.AddSeconds(-SECONDS_BEFORE_DETECTION);
            var endTime = detectionTime.AddSeconds(SECONDS_AFTER_DETECTION);
            var duration = (endTime - startTime).TotalSeconds;

            return (startTime, endTime, duration);
        }

        private async Task ExtractRegularFrames(string outputDir, (DateTime StartTime, DateTime EndTime, double Duration) timeWindow, int totalFrames, double frameInterval)
        {
            var media = await FindMediaForTimeWindow(timeWindow);
            if (media == null)
                return;

            string videoPath = GetMediaFilePath(media);
            if (!File.Exists(videoPath))
                return;

            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
                return;

            double fps = capture.Fps > 0 ? capture.Fps : 20.0;
            DateTime videoStartTime = media.Timestamp;

            for (int i = 0; i < totalFrames; i++)
            {
                var frameTime = timeWindow.StartTime.AddSeconds(i * frameInterval);
                var framePath = Path.Combine(outputDir, $"frame_{i:000}.jpg");

                double secondsFromVideoStart = (frameTime - videoStartTime).TotalSeconds;
                if (secondsFromVideoStart < 0)
                    continue;

                int targetFrameNumber = (int)(secondsFromVideoStart * fps);

                capture.PosFrames = targetFrameNumber;
                using var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty())
                    continue;

                DrawTrackersOnFrame(frame, frameTime);
                var encodingParams = new int[] { (int)ImwriteFlags.JpegQuality, 90 };
                Cv2.ImEncode(".jpg", frame, out byte[] jpegBytes, encodingParams);
                await SaveFrameToFile(jpegBytes, framePath, frameTime);

                UpdateActiveTrackers(frame, frameTime, media.MediaId);

                if (i % 10 == 0)
                    await _context.SaveChangesAsync();
            }
        }

        private async Task SaveKeyFrames(string outputDir, Detection detection)
        {
            if (!detection.MediaId.HasValue)
            {
                return;
            }
            var media = await LoadAndValidateMediaAsync(detection.MediaId.Value);
            string videoPath = GetMediaFilePath(media);

            if (!File.Exists(videoPath))
            {
                return;
            }

            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
                return;

            double fps = capture.Fps > 0 ? capture.Fps : 20.0;
            DateTime videoStartTime = media.Timestamp;

            var keyFrames = new[]
            {
                ("detection_moment.jpg", detection.DetectedAt),
                ("before_detection.jpg", detection.DetectedAt.AddSeconds(-5)),
                ("after_detection.jpg", detection.DetectedAt.AddSeconds(5))
            };

            foreach (var (fileName, frameTime) in keyFrames)
            {
                var keyFramePath = Path.Combine(outputDir, fileName);

                double secondsFromVideoStart = (frameTime - videoStartTime).TotalSeconds;
                if (secondsFromVideoStart < 0)
                {
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

        private async Task<byte[]> ExtractFrameAtPosition(VideoCapture capture, int frameNumber, DateTime frameTime, int? contextId = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    capture.PosFrames = frameNumber;
                    using var frame = new Mat();
                    bool readSuccess = capture.Read(frame);

                    if (!readSuccess || frame.Empty())
                    {
                        throw new InvalidOperationException($"Failed to read frame {frameNumber} at {frameTime}");
                    }

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
                    throw;
                }
            });
        }

        private async Task SaveFrameToFile(byte[] frameData, string framePath, DateTime frameTime)
        {
            if (frameData == null || frameData.Length == 0)
                return;

            try
            {
                string directory = Path.GetDirectoryName(framePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(framePath, frameData);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async Task<Media> FindMediaForTimeWindow((DateTime StartTime, DateTime EndTime, double Duration) timeWindow)
        {
            var media = await _context.Media
                .Where(m => m.Timestamp <= timeWindow.StartTime && m.Type == "video")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();

            return media;
        }

        private string GetMediaFilePath(Media media)
        {
            if (Path.IsPathRooted(media.FilePath))
            {
                return media.FilePath;
            }
            else
            {
                return Path.Combine(_environment.ContentRootPath, media.FilePath);
            }
        }

        #endregion

        #region Private Methods - TRACKING

        private async Task LogStatusIfNeeded()
        {
            if ((DateTime.UtcNow - _lastStatusLog).TotalSeconds >= STATUS_LOG_INTERVAL_SECONDS)
            {
                await LogTrackingStatusSummaryAsync();
                _lastStatusLog = DateTime.UtcNow;
            }
        }

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

        private async Task<bool> ShouldSaveDetection(Detection detection)
        {
            var cutoffTime = DateTime.UtcNow.AddSeconds(-2);

            var recentDetection = await _context.Detections
                .Where(d => d.DeviceId == detection.DeviceId &&
                           d.DetectedObject == detection.DetectedObject &&
                           d.DetectedAt > cutoffTime)
                .OrderByDescending(d => d.DetectedAt)
                .FirstOrDefaultAsync();

            if (recentDetection == null)
            {
                await _logService.AddLogAsync(1, "DetectionApproved",
                    $"No recent detection found - will save {detection.DetectedObject}",
                    "Info", null);
                return true;
            }

            if (detection.Confidence > recentDetection.Confidence + 10)
            {
                //  Use recentDetection.DetectionId which definitely exists
                await _logService.AddLogAsync(1, "HigherConfidenceDetection",
                    $"Saving higher confidence detection: {detection.Confidence:F1}% vs {recentDetection.Confidence:F1}%",
                    "Info", recentDetection.DetectionId);

                return true;
            }
            return false;
        }

        private async Task<int> SaveDetectionFrameAsync(byte[] frameBytes, int deviceId)
        {
            try
            {
                await _logService.AddLogAsync(1, "SaveFrame_Start",
                    $"SaveDetectionFrameAsync started (DeviceId: {deviceId}, FrameSize: {frameBytes?.Length ?? 0})",
                    "Debug", null);

                string fileName = $"detection_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
                string relativePath = Path.Combine("Media", "HighlightFrames", fileName);
                string fullPath = Path.Combine(_environment.ContentRootPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                await File.WriteAllBytesAsync(fullPath, frameBytes);

                var media = new Media
                {
                    DeviceId = deviceId,
                    Type = "Frame",
                    FilePath = relativePath,
                    Timestamp = DateTime.Now
                };

                _context.Media.Add(media);
                await _context.SaveChangesAsync();

                await _logService.AddLogAsync(1, "SaveFrame_Success",
                    $" SaveDetectionFrameAsync completed successfully! MediaId={media.MediaId}",
                    "Info", null);

                return media.MediaId;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "SaveFrame_Error",
                    $"ERROR in SaveDetectionFrameAsync: {ex.Message}\nStack: {ex.StackTrace}",
                    "Error", null);
                throw;
            }
        }

        // ⭐ FIXED: Don't log with detectionId until AFTER SaveChangesAsync
        private async Task InitializeOrUpdateTrackingForSaving(Detection detection)
        {
            try
            {
                await _logService.AddLogAsync(1, "Tracking_Start",
                    $"InitializeOrUpdateTrackingForSaving started for {detection.DetectedObject}",
                    "Debug", null);

                var newBox = new Rect(
                    (int)detection.BoundingBoxX,
                    (int)detection.BoundingBoxY,
                    (int)detection.BoundingBoxWidth,
                    (int)detection.BoundingBoxHeight
                );

                int? matchedId = FindMatchingTracker(newBox);
                if (matchedId.HasValue)
                {
                    _lastSeenTimes[matchedId.Value] = DateTime.UtcNow;
                    _lastKnownBounds[matchedId.Value] = newBox;
                    detection.TrackingId = matchedId.Value;

                    await _logService.AddLogAsync(1, "Tracking_MatchedExisting",
                        $"Matched existing tracker: TrackingId={matchedId.Value}",
                        "Debug", null);
                }
                else
                {
                    int newId = await GetNextTrackingId();
                    _lastSeenTimes[newId] = DateTime.UtcNow;
                    _lastKnownBounds[newId] = newBox;
                    _trackerObjects[newId] = detection.DetectedObject ?? "Unknown";
                    detection.TrackingId = newId;

                    await _logService.AddLogAsync(1, "TrackingIdAssigned",
                        $"Assigned NEW tracking ID {detection.TrackingId} to detection of {detection.DetectedObject}",
                        "Info", null);
                }

                await _logService.AddLogAsync(1, "Tracking_AddingToDb",
                    $"About to add Detection to database (DeviceId: {detection.DeviceId}, MediaId: {detection.MediaId}, EventId: {detection.EventId})",
                    "Debug", null);

                _context.Detections.Add(detection);

                await _logService.AddLogAsync(1, "Tracking_SavingToDb",
                    $"About to call SaveChangesAsync",
                    "Debug", null);

                // ⭐ SAVE FIRST - this assigns detection.DetectionId
                await _context.SaveChangesAsync();

                //  NOW it's safe to log with detection.DetectionId
                await _logService.AddLogAsync(1, "Tracking_Success",
                    $" Detection saved to database! DetectionId={detection.DetectionId}",
                    "Info", detection.DetectionId);

                if (!matchedId.HasValue)
                {
                    //  NOW it's safe to log with detection.DetectionId
                    await _logService.AddLogAsync(1, "TrackerCreated",
                        $"Created new tracker {detection.TrackingId} for detection of {detection.DetectedObject}",
                        "Info", detection.DetectionId);
                }
            }
            catch (Exception ex)
            {
                //  NULL detectionId - detection might not exist yet
                await _logService.AddLogAsync(1, "Tracking_Error",
                    $"ERROR in InitializeOrUpdateTrackingForSaving: {ex.Message}\n" +
                    $"Inner Exception: {ex.InnerException?.Message}\n" +
                    $"Stack: {ex.StackTrace}",
                    "Error", null);
                throw;
            }
        }

        private int? FindMatchingTracker(Rect newBox)
        {
            foreach (var kvp in _lastKnownBounds)
            {
                double overlap = CalculateIoU(newBox, kvp.Value);
                if (overlap >= MIN_OVERLAP_THRESHOLD)
                    return kvp.Key;
            }
            return null;
        }

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

        private async void UpdateActiveTrackers(Mat frame, DateTime frameTime, int mediaId)
        {
            var trackingDetections = new List<Detection>();
            foreach (var kvp in _activeTrackers.ToList())
            {
                var tracker = kvp.Value;
                var id = kvp.Key;

                var newBox = tracker.Update(frame);
                if (newBox.HasValue)
                {
                    _lastKnownBounds[id] = newBox.Value;
                    _lastSeenTimes[id] = DateTime.UtcNow;

                    var trackingDetection = new Detection
                    {
                        MediaId = mediaId,
                        TrackingId = id,
                        DetectedAt = frameTime,
                        BoundingBoxX = newBox.Value.X,
                        BoundingBoxY = newBox.Value.Y,
                        BoundingBoxWidth = newBox.Value.Width,
                        BoundingBoxHeight = newBox.Value.Height,
                        Confidence = 1.0f,
                        DetectedObject = _trackerObjects.ContainsKey(id) ? _trackerObjects[id] : "Unknown"
                    };
                    trackingDetections.Add(trackingDetection);
                }
                else
                {
                    tracker.Dispose();
                    _activeTrackers.Remove(id);
                    _lastSeenTimes.Remove(id);
                    _lastKnownBounds.Remove(id);
                    _trackerObjects.Remove(id);

                    _ = Task.Run(async () => await _logService.AddLogAsync(1, "TrackingLost",
                                   $"Tracker {id} removed at {frameTime}", "Warning", null));
                }
            }
            if (trackingDetections.Any())
            {
                try
                {
                    _context.Detections.AddRange(trackingDetections);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    await _logService.AddLogAsync(1, "TrackingDetectionSaveFailed",
                        $"Failed to save {trackingDetections.Count} tracking detections: {ex.Message}",
                        "Error", null);
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
using ZooTrack.Data;
using ZooTrack.Models;
using Microsoft.EntityFrameworkCore;
using ZooTrackBackend.Services;

namespace ZooTrack.Services
{
    /* This service will:
             *      1. listen to detection events
             *      2. extract frames from media at low fps
             *      3. save only those frames into disk
             *      4. register them into Media table connected to the Detection
             */

    public class DetectionMediaService
    {
        private readonly ZootrackDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogService _logService;

        // TRACKING CONFIGURATION
        private const int FRAMES_PER_SECOND = 5;           // Save 5 frames per second
        private const int SECONDS_BEFORE_DETECTION = 10;   // Save 10 seconds before
        private const int SECONDS_AFTER_DETECTION = 30;    // Save 30 seconds after
        private const double SAME_OBJECT_TIME_THRESHOLD = 15.0; // 15 seconds
        private const double SAME_OBJECT_DISTANCE_THRESHOLD = 0.3; // 30% of frame

        public DetectionMediaService(ZootrackDbContext context, IWebHostEnvironment environment, ILogService logService)
        {
            _context = context;
            _environment = environment;
            _logService = logService;
        }

        public async Task ExtractFramesAsync(Detection detection)
        {
            try
            {
                await _logService.AddLogAsync(1, "FrameExtractionStarted",
                    $"Starting smart frame extraction for detection {detection.DetectionId}", "Info", detection.DetectionId);

                // Load media
                var media = await _context.Media.FindAsync(detection.MediaId);
                if (media == null)
                {
                    throw new Exception($"Media with ID {detection.MediaId} not found.");
                }

                var mediaPath = Path.Combine(_environment.ContentRootPath, "MediaFiles", media.FilePath);
                if (!File.Exists(mediaPath))
                {
                    throw new Exception($"Media file not found: {mediaPath}");
                }

                // Create output directory
                var outputDir = Path.Combine(_environment.ContentRootPath, "SavedDetections", detection.DetectionId.ToString());
                Directory.CreateDirectory(outputDir);

                // Extract frames for tracking
                await ExtractTrackingFramesAsync(mediaPath, outputDir, detection);

                // Try to correlate with existing detections (find same object)
                await CorrelateWithExistingDetections(detection);

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

        private async Task ExtractTrackingFramesAsync(string videoPath, string outputDir, Detection detection)
        {
            try
            {
                // SMART FRAME EXTRACTION LOGIC

                // Calculate time window
                var detectionTime = detection.DetectedAt;
                var startTime = detectionTime.AddSeconds(-SECONDS_BEFORE_DETECTION);
                var endTime = detectionTime.AddSeconds(SECONDS_AFTER_DETECTION);
                var totalDuration = (endTime - startTime).TotalSeconds;

                // Calculate how many frames to extract
                var totalFramesToExtract = (int)(totalDuration * FRAMES_PER_SECOND);
                var frameInterval = 1.0 / FRAMES_PER_SECOND; // Time between frames in seconds

                await _logService.AddLogAsync(1, "FrameExtractionPlan",
                    $"Extracting {totalFramesToExtract} frames at {FRAMES_PER_SECOND} FPS for detection {detection.DetectionId}",
                    "Info", detection.DetectionId);

                // PLACEHOLDER FOR ACTUAL FRAME EXTRACTION
                // In real implementation, you would use FFmpeg or OpenCV here

                // Simulate frame extraction
                for (int i = 0; i < totalFramesToExtract; i++)
                {
                    var frameTime = startTime.AddSeconds(i * frameInterval);
                    var framePath = Path.Combine(outputDir, $"frame_{i:000}.jpg");

                    // SIMULATE: In real code, extract actual frame here
                    await SimulateFrameExtraction(framePath, frameTime);
                }

                // Save KEY FRAMES (most important ones)
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

        private async Task SaveKeyFrames(string outputDir, Detection detection)
        {
            // Save the most important frames
            var keyFrames = new[]
            {
                "detection_moment.jpg",    // Exact moment of detection
                "before_detection.jpg",    // 5 seconds before
                "after_detection.jpg"      // 5 seconds after
            };

            foreach (var keyFrame in keyFrames)
            {
                var keyFramePath = Path.Combine(outputDir, keyFrame);
                await SimulateFrameExtraction(keyFramePath, detection.DetectedAt);
            }
        }

        private async Task SimulateFrameExtraction(string framePath, DateTime frameTime)
        {
            // PLACEHOLDER - In real implementation, use FFmpeg or OpenCV
            // For now, just create an empty file to simulate
            await File.WriteAllTextAsync(framePath, $"Frame extracted at {frameTime:yyyy-MM-dd HH:mm:ss}");
            await Task.Delay(10); // Simulate processing time
        }

        // NEW METHOD: Correlate detections to find same objects
        private async Task CorrelateWithExistingDetections(Detection newDetection)
        {
            try
            {
                // Find recent detections from same device
                var recentDetections = await _context.Detections
                    .Where(d => d.DeviceId == newDetection.DeviceId &&
                               d.DetectionId != newDetection.DetectionId &&
                               d.DetectedAt >= newDetection.DetectedAt.AddSeconds(-SAME_OBJECT_TIME_THRESHOLD) &&
                               d.DetectedAt <= newDetection.DetectedAt.AddSeconds(SAME_OBJECT_TIME_THRESHOLD))
                    .OrderBy(d => Math.Abs((d.DetectedAt - newDetection.DetectedAt).TotalSeconds))
                    .ToListAsync();

                foreach (var existingDetection in recentDetections)
                {
                    if (IsSameObject(newDetection, existingDetection))
                    {
                        // Assign same tracking ID
                        if (existingDetection.TrackingId.HasValue)
                        {
                            newDetection.TrackingId = existingDetection.TrackingId;
                        }
                        else
                        {
                            // Create new tracking ID for both
                            var newTrackingId = await GetNextTrackingId();
                            newDetection.TrackingId = newTrackingId;
                            existingDetection.TrackingId = newTrackingId;
                            _context.Detections.Update(existingDetection);
                        }

                        await _logService.AddLogAsync(1, "ObjectTracked",
                            $"Detection {newDetection.DetectionId} correlated with detection {existingDetection.DetectionId} (TrackingId: {newDetection.TrackingId})",
                            "Info", newDetection.DetectionId);

                        break; // Found match, stop looking
                    }
                }

                // If no match found, this might be a new object
                if (!newDetection.TrackingId.HasValue)
                {
                    newDetection.TrackingId = await GetNextTrackingId();
                    await _logService.AddLogAsync(1, "NewObjectDetected",
                        $"New object detected with TrackingId: {newDetection.TrackingId}",
                        "Info", newDetection.DetectionId);
                }
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "CorrelationError",
                    $"Error correlating detections: {ex.Message}", "Error", newDetection.DetectionId);
            }
        }

        private bool IsSameObject(Detection detection1, Detection detection2)
        {
            // Check time difference
            var timeDiff = Math.Abs((detection1.DetectedAt - detection2.DetectedAt).TotalSeconds);
            if (timeDiff > SAME_OBJECT_TIME_THRESHOLD)
                return false;

            // Check if bounding boxes are set
            if (detection1.BoundingBoxWidth == 0 || detection2.BoundingBoxWidth == 0)
                return false; // Can't compare without bounding box data

            // Calculate distance between centers
            var center1X = detection1.BoundingBoxX + detection1.BoundingBoxWidth / 2;
            var center1Y = detection1.BoundingBoxY + detection1.BoundingBoxHeight / 2;
            var center2X = detection2.BoundingBoxX + detection2.BoundingBoxWidth / 2;
            var center2Y = detection2.BoundingBoxY + detection2.BoundingBoxHeight / 2;

            var distance = Math.Sqrt(Math.Pow(center1X - center2X, 2) + Math.Pow(center1Y - center2Y, 2));

            // Check if distance is within threshold
            if (distance > SAME_OBJECT_DISTANCE_THRESHOLD)
                return false;

            // Check size similarity (objects shouldn't change size dramatically)
            var size1 = detection1.BoundingBoxWidth * detection1.BoundingBoxHeight;
            var size2 = detection2.BoundingBoxWidth * detection2.BoundingBoxHeight;
            var sizeRatio = Math.Min(size1, size2) / Math.Max(size1, size2);

            return sizeRatio > 0.7; // Objects should be similar size (70% similarity)
        }

        private async Task<int> GetNextTrackingId()
        {
            var maxTrackingId = await _context.Detections
                .Where(d => d.TrackingId.HasValue)
                .MaxAsync(d => (int?)d.TrackingId) ?? 0;

            return maxTrackingId + 1;
        }
    }
}

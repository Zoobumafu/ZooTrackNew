using ZooTrack.Data;
using ZooTrack.Models;
using Microsoft.EntityFrameworkCore;
using ZooTrackBackend.Services;

namespace ZooTrack.Services
{
    /// <summary>
    /// Service responsible for extracting frames from media files during detection events
    /// and correlating detections to track the same objects across multiple frames.
    /// This service provides intelligent frame extraction and object tracking capabilities.
    /// </summary>
    /// <remarks>
    /// The service performs the following operations:
    /// 1. Listens to detection events
    /// 2. Extracts frames from media at configurable frame rates
    /// 3. Saves extracted frames to disk storage
    /// 4. Registers frames in the Media table connected to detections
    /// 5. Correlates detections to identify and track the same objects
    /// </remarks>
    public class DetectionMediaService
    {
        #region Constants and Configuration

        /// Number of frames to extract per second for tracking analysis
        private const int FRAMES_PER_SECOND = 5;

        /// Number of seconds before the detection moment to include in frame extraction
        private const int SECONDS_BEFORE_DETECTION = 10;

        /// Number of seconds after the detection moment to include in frame extraction
        private const int SECONDS_AFTER_DETECTION = 30;

        /// Maximum time difference in seconds between detections to consider them as the same object
        private const double SAME_OBJECT_TIME_THRESHOLD = 15.0;

        /// Maximum distance threshold (as percentage of frame) between detection centers to consider them as the same object
        private const double SAME_OBJECT_DISTANCE_THRESHOLD = 0.3;

        /// Minimum size similarity ratio required for detections to be considered the same object
        private const double SIZE_SIMILARITY_THRESHOLD = 0.7;

        #endregion

        #region Fields

        private readonly ZootrackDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogService _logService;

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

                // Validate and load media file
                var media = await LoadAndValidateMediaAsync(detection.MediaId);
                var mediaPath = GetMediaPath(media.FilePath);

                // Prepare output directory for extracted frames
                var outputDir = CreateOutputDirectory(detection.DetectionId);

                // Extract frames for tracking analysis
                await ExtractTrackingFramesAsync(mediaPath, outputDir, detection);

                // Correlate with existing detections to identify same objects
                await CorrelateWithExistingDetections(detection);

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

        /// <summary>
        /// Extracts frames from a video file at specified intervals around the detection time.
        /// Creates both tracking frames and key frames for analysis.
        /// </summary>
        /// <param name="videoPath">Full path to the source video file</param>
        /// <param name="outputDir">Directory where extracted frames will be saved</param>
        /// <param name="detection">The detection event providing timing information</param>
        /// <returns>A task representing the asynchronous frame extraction operation</returns>
        private async Task ExtractTrackingFramesAsync(string videoPath, string outputDir, Detection detection)
        {
            try
            {
                // Calculate the time window for frame extraction
                var timeWindow = CalculateExtractionTimeWindow(detection.DetectedAt);
                var totalFramesToExtract = (int)(timeWindow.Duration * FRAMES_PER_SECOND);
                var frameInterval = 1.0 / FRAMES_PER_SECOND;

                await _logService.AddLogAsync(1, "FrameExtractionPlan",
                    $"Extracting {totalFramesToExtract} frames at {FRAMES_PER_SECOND} FPS for detection {detection.DetectionId}",
                    "Info", detection.DetectionId);

                // Extract frames at regular intervals
                await ExtractRegularFrames(outputDir, timeWindow, totalFramesToExtract, frameInterval);

                // Extract and save key frames (most important moments)
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

        /// <summary>
        /// Calculates the time window for frame extraction based on detection time and configuration.
        /// </summary>
        /// <param name="detectionTime">The moment when the detection occurred</param>
        /// <returns>A tuple containing start time, end time, and total duration</returns>
        private (DateTime StartTime, DateTime EndTime, double Duration) CalculateExtractionTimeWindow(DateTime detectionTime)
        {
            var startTime = detectionTime.AddSeconds(-SECONDS_BEFORE_DETECTION);
            var endTime = detectionTime.AddSeconds(SECONDS_AFTER_DETECTION);
            var duration = (endTime - startTime).TotalSeconds;

            return (startTime, endTime, duration);
        }

        /// <summary>
        /// Extracts frames at regular intervals within the specified time window.
        /// </summary>
        /// <param name="outputDir">Directory where frames will be saved</param>
        /// <param name="timeWindow">The time window for extraction</param>
        /// <param name="totalFrames">Total number of frames to extract</param>
        /// <param name="frameInterval">Time interval between frames in seconds</param>
        /// <returns>A task representing the asynchronous extraction operation</returns>
        private async Task ExtractRegularFrames(string outputDir, (DateTime StartTime, DateTime EndTime, double Duration) timeWindow, int totalFrames, double frameInterval)
        {
            for (int i = 0; i < totalFrames; i++)
            {
                var frameTime = timeWindow.StartTime.AddSeconds(i * frameInterval);
                var framePath = Path.Combine(outputDir, $"frame_{i:000}.jpg");

                // TODO: Replace with actual frame extraction using FFmpeg or OpenCV
                await SimulateFrameExtraction(framePath, frameTime);
            }
        }

        /// <summary>
        /// Saves key frames that represent the most important moments during detection.
        /// These include the exact detection moment and frames before/after for context.
        /// </summary>
        /// <param name="outputDir">Directory where key frames will be saved</param>
        /// <param name="detection">The detection event providing timing information</param>
        /// <returns>A task representing the asynchronous key frame saving operation</returns>
        private async Task SaveKeyFrames(string outputDir, Detection detection)
        {
            var keyFrames = new[]
            {
                ("detection_moment.jpg", detection.DetectedAt),
                ("before_detection.jpg", detection.DetectedAt.AddSeconds(-5)),
                ("after_detection.jpg", detection.DetectedAt.AddSeconds(5))
            };

            foreach (var (fileName, frameTime) in keyFrames)
            {
                var keyFramePath = Path.Combine(outputDir, fileName);
                await SimulateFrameExtraction(keyFramePath, frameTime);
            }
        }

        /// <summary>
        /// Simulates frame extraction by creating placeholder files.
        /// In production, this should be replaced with actual FFmpeg or OpenCV frame extraction.
        /// </summary>
        /// <param name="framePath">Path where the extracted frame will be saved</param>
        /// <param name="frameTime">The timestamp of the frame being extracted</param>
        /// <returns>A task representing the asynchronous simulation operation</returns>
        /// <remarks>
        /// This is a placeholder implementation. In a real system, you would use:
        /// - FFmpeg command-line tool for video frame extraction
        /// - OpenCV for programmatic video processing
        /// - Other video processing libraries
        /// </remarks>
        private async Task SimulateFrameExtraction(string framePath, DateTime frameTime)
        {
            await File.WriteAllTextAsync(framePath, $"Frame extracted at {frameTime:yyyy-MM-dd HH:mm:ss}");
            await Task.Delay(10); // Simulate processing time
        }

        #endregion

        #region Private Methods - Object Correlation and Tracking

        /// <summary>
        /// Correlates a new detection with existing detections to identify if they represent the same object.
        /// Assigns tracking IDs to group related detections together.
        /// </summary>
        /// <param name="newDetection">The new detection to correlate with existing ones</param>
        /// <returns>A task representing the asynchronous correlation operation</returns>
        private async Task CorrelateWithExistingDetections(Detection newDetection)
        {
            try
            {
                // Find recent detections from the same device within time threshold
                var recentDetections = await FindRecentDetections(newDetection);

                // Check each recent detection for correlation
                foreach (var existingDetection in recentDetections)
                {
                    if (IsSameObject(newDetection, existingDetection))
                    {
                        await AssignTrackingId(newDetection, existingDetection);

                        await _logService.AddLogAsync(1, "ObjectTracked",
                            $"Detection {newDetection.DetectionId} correlated with detection {existingDetection.DetectionId} (TrackingId: {newDetection.TrackingId})",
                            "Info", newDetection.DetectionId);

                        return; // Found match, stop looking
                    }
                }

                // If no match found, assign new tracking ID for new object
                await AssignNewTrackingId(newDetection);
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "CorrelationError",
                    $"Error correlating detections: {ex.Message}", "Error", newDetection.DetectionId);
            }
        }

        /// <summary>
        /// Finds recent detections from the same device within the correlation time threshold.
        /// </summary>
        /// <param name="newDetection">The new detection to find correlations for</param>
        /// <returns>A list of recent detections ordered by time proximity</returns>
        private async Task<List<Detection>> FindRecentDetections(Detection newDetection)
        {
            return await _context.Detections
                .Where(d => d.DeviceId == newDetection.DeviceId &&
                           d.DetectionId != newDetection.DetectionId &&
                           d.DetectedAt >= newDetection.DetectedAt.AddSeconds(-SAME_OBJECT_TIME_THRESHOLD) &&
                           d.DetectedAt <= newDetection.DetectedAt.AddSeconds(SAME_OBJECT_TIME_THRESHOLD))
                .OrderBy(d => Math.Abs((d.DetectedAt - newDetection.DetectedAt).TotalSeconds))
                .ToListAsync();
        }

        /// <summary>
        /// Determines if two detections represent the same object based on timing, position, and size similarity.
        /// </summary>
        /// <param name="detection1">First detection to compare</param>
        /// <param name="detection2">Second detection to compare</param>
        /// <returns>True if the detections likely represent the same object, false otherwise</returns>
        private bool IsSameObject(Detection detection1, Detection detection2)
        {
            // Check time difference constraint
            if (!IsWithinTimeThreshold(detection1, detection2))
                return false;

            // Check if bounding box data is available
            if (!HasValidBoundingBoxes(detection1, detection2))
                return false;

            // Check spatial proximity of detection centers
            if (!IsWithinDistanceThreshold(detection1, detection2))
                return false;

            // Check size similarity between detections
            return IsSimilarSize(detection1, detection2);
        }

        /// <summary>
        /// Checks if two detections occurred within the acceptable time threshold.
        /// </summary>
        /// <param name="detection1">First detection</param>
        /// <param name="detection2">Second detection</param>
        /// <returns>True if detections are within time threshold</returns>
        private bool IsWithinTimeThreshold(Detection detection1, Detection detection2)
        {
            var timeDiff = Math.Abs((detection1.DetectedAt - detection2.DetectedAt).TotalSeconds);
            return timeDiff <= SAME_OBJECT_TIME_THRESHOLD;
        }

        /// <summary>
        /// Validates that both detections have valid bounding box data for comparison.
        /// </summary>
        /// <param name="detection1">First detection</param>
        /// <param name="detection2">Second detection</param>
        /// <returns>True if both detections have valid bounding boxes</returns>
        private bool HasValidBoundingBoxes(Detection detection1, Detection detection2)
        {
            return detection1.BoundingBoxWidth > 0 && detection2.BoundingBoxWidth > 0;
        }

        /// <summary>
        /// Checks if the centers of two detection bounding boxes are within the distance threshold.
        /// </summary>
        /// <param name="detection1">First detection</param>
        /// <param name="detection2">Second detection</param>
        /// <returns>True if detection centers are close enough to be considered the same object</returns>
        private bool IsWithinDistanceThreshold(Detection detection1, Detection detection2)
        {
            var center1 = CalculateBoundingBoxCenter(detection1);
            var center2 = CalculateBoundingBoxCenter(detection2);
            var distance = CalculateDistance(center1, center2);

            return distance <= SAME_OBJECT_DISTANCE_THRESHOLD;
        }

        /// <summary>
        /// Calculates the center point of a detection's bounding box.
        /// </summary>
        /// <param name="detection">The detection whose center to calculate</param>
        /// <returns>A tuple containing the X and Y coordinates of the center</returns>
        private (double X, double Y) CalculateBoundingBoxCenter(Detection detection)
        {
            var centerX = detection.BoundingBoxX + detection.BoundingBoxWidth / 2.0;
            var centerY = detection.BoundingBoxY + detection.BoundingBoxHeight / 2.0;
            return (centerX, centerY);
        }

        /// <summary>
        /// Calculates the Euclidean distance between two points.
        /// </summary>
        /// <param name="point1">First point coordinates</param>
        /// <param name="point2">Second point coordinates</param>
        /// <returns>The distance between the two points</returns>
        private double CalculateDistance((double X, double Y) point1, (double X, double Y) point2)
        {
            return Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
        }

        /// <summary>
        /// Checks if two detections have similar sizes (bounding box areas).
        /// </summary>
        /// <param name="detection1">First detection</param>
        /// <param name="detection2">Second detection</param>
        /// <returns>True if the detections have similar sizes</returns>
        private bool IsSimilarSize(Detection detection1, Detection detection2)
        {
            var size1 = detection1.BoundingBoxWidth * detection1.BoundingBoxHeight;
            var size2 = detection2.BoundingBoxWidth * detection2.BoundingBoxHeight;
            var sizeRatio = Math.Min(size1, size2) / Math.Max(size1, size2);

            return sizeRatio > SIZE_SIMILARITY_THRESHOLD;
        }

        /// <summary>
        /// Assigns a tracking ID to a new detection based on an existing correlated detection.
        /// </summary>
        /// <param name="newDetection">The new detection to assign a tracking ID to</param>
        /// <param name="existingDetection">The existing detection to correlate with</param>
        /// <returns>A task representing the asynchronous tracking ID assignment</returns>
        private async Task AssignTrackingId(Detection newDetection, Detection existingDetection)
        {
            if (existingDetection.TrackingId.HasValue)
            {
                // Use existing tracking ID
                newDetection.TrackingId = existingDetection.TrackingId;
            }
            else
            {
                // Create new tracking ID for both detections
                var newTrackingId = await GetNextTrackingId();
                newDetection.TrackingId = newTrackingId;
                existingDetection.TrackingId = newTrackingId;
                _context.Detections.Update(existingDetection);
            }
        }

        /// <summary>
        /// Assigns a new tracking ID to a detection that doesn't correlate with any existing detections.
        /// </summary>
        /// <param name="newDetection">The detection to assign a new tracking ID to</param>
        /// <returns>A task representing the asynchronous new tracking ID assignment</returns>
        private async Task AssignNewTrackingId(Detection newDetection)
        {
            newDetection.TrackingId = await GetNextTrackingId();
            await _logService.AddLogAsync(1, "NewObjectDetected",
                $"New object detected with TrackingId: {newDetection.TrackingId}",
                "Info", newDetection.DetectionId);
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

        #endregion
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrackBackend.Services;

namespace ZooTrack.Services
{
    /// <summary>
    /// Service responsible for managing detection operations including creation, validation, 
    /// tracking, and correlation with related entities (devices, media, events).
    /// Provides comprehensive logging and notification capabilities for detection events.
    /// </summary>
    /// <remarks>
    /// This service handles:
    /// - Detection creation and validation
    /// - Foreign key relationship management
    /// - Confidence-based alerting and notifications
    /// - Frame extraction and object tracking integration
    /// - Comprehensive audit logging for all detection operations
    /// </remarks>
    public class DetectionService : IDetectionService
    {
        #region Constants

        /// Confidence threshold for critical risk detections requiring immediate attention
        private const double CRITICAL_CONFIDENCE_RISK = 95.0;

        /// Confidence threshold for high risk detections that trigger notifications
        private const double HIGH_CONFIDENCE_RISK = 90.0;

        /// Confidence threshold for moderate risk detections requiring monitoring
        private const double MODERATE_CONFIDENCE_RISK = 80.0;

        /// Default system user ID for service-level operations
        private const int SYSTEM_USER_ID = 1;


        /// Time window in minutes for detecting frequent detections from the same device
        private const int FREQUENT_DETECTION_WINDOW_MINUTES = 10;

        /// Number of detections within the time window that triggers a frequent detection alert
        private const int FREQUENT_DETECTION_THRESHOLD = 5;

        /// Default event duration in hours for automatically created events
        private const int DEFAULT_EVENT_DURATION_HOURS = 24;

        #endregion

        #region Fields

        private readonly ZootrackDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ILogService _logService;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DetectionService class.
        /// </summary>
        /// <param name="context">Database context for accessing detection and related data</param>
        /// <param name="notificationService">Service for sending notifications about detections</param>
        /// <param name="logService">Service for logging detection operations and events</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
        public DetectionService(ZootrackDbContext context, NotificationService notificationService, ILogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new detection with comprehensive validation, relationship management, and logging.
        /// Automatically handles missing foreign key relationships and triggers appropriate notifications.
        /// </summary>
        /// <param name="detection">The detection object to create</param>
        /// <returns>The created detection with all relationships properly established</returns>
        /// <exception cref="ArgumentNullException">Thrown when detection parameter is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when required foreign key relationships cannot be established</exception>
        public async Task<Detection> CreateDetectionAsync(Detection detection)
        {
            if (detection == null)
                throw new ArgumentNullException(nameof(detection), "Detection cannot be null");

            try
            {
                // Initialize detection with default values
                await InitializeDetectionDefaults(detection);

                // Ensure all required foreign key relationships exist
                await EnsureRequiredRelationships(detection);

                // Validate that all foreign key references exist in database
                await ValidateForeignKeyReferences(detection);

                // Save detection to database
                _context.Detections.Add(detection);
                await _context.SaveChangesAsync();

                // Log detection creation with appropriate severity level
                await LogDetectionCreation(detection);

                // Handle high-confidence detection notifications and alerts
                await ProcessHighConfidenceDetection(detection);

                // Check for frequent detections that might indicate system issues
                await CheckForFrequentDetections(detection);

                return detection;
            }
            catch (Exception ex)
            {
                await LogDetectionCreationFailure(detection, ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a detection with enhanced tracking information including bounding box coordinates
        /// and automatic frame extraction for object tracking analysis.
        /// </summary>
        /// <param name="detection">The base detection object to create</param>
        /// <param name="boundingBoxX">X coordinate of the detection bounding box</param>
        /// <param name="boundingBoxY">Y coordinate of the detection bounding box</param>
        /// <param name="boundingBoxWidth">Width of the detection bounding box</param>
        /// <param name="boundingBoxHeight">Height of the detection bounding box</param>
        /// <param name="detectedObject">Optional description of the detected object</param>
        /// <returns>The created detection with tracking information and frame extraction initiated</returns>
        /// <exception cref="ArgumentNullException">Thrown when detection parameter is null</exception>
        public async Task<Detection> CreateDetectionWithTrackingAsync(Detection detection,
    float boundingBoxX, float boundingBoxY, float boundingBoxWidth, float boundingBoxHeight,
    string detectedObject = null)
        {
            if (detection == null)
                throw new ArgumentNullException(nameof(detection));

            try
            {
                // Set tracking-specific information
                await PopulateTrackingInformation(detection, boundingBoxX, boundingBoxY,
                    boundingBoxWidth, boundingBoxHeight, detectedObject);

                // Create the detection using standard creation process
                var createdDetection = await CreateDetectionAsync(detection);

                // NEW: Try generating the route based on the tracking ID
                if (createdDetection.TrackingId.HasValue)
                {
                    await TryGenerateTrackingRouteAsync(
                        createdDetection.TrackingId.Value,
                        createdDetection.DeviceId);
                }

                // Initiate frame extraction for tracking analysis (non-blocking)
                await InitiateFrameExtraction(createdDetection);

                return createdDetection;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(SYSTEM_USER_ID, "TrackingDetectionCreationFailed",
                    $"Failed to create detection with tracking: {ex.Message}", "Error");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all detections for a specific device with related entity data and analytics logging.
        /// </summary>
        /// <param name="deviceId">The ID of the device to retrieve detections for</param>
        /// <returns>A collection of detections for the specified device, ordered by detection time (newest first)</returns>
        /// <exception cref="ArgumentException">Thrown when deviceId is invalid</exception>
        public async Task<IEnumerable<Detection>> GetDetectionsForDeviceAsync(int deviceId)
        {
            if (deviceId <= 0)
                throw new ArgumentException("Device ID must be greater than zero", nameof(deviceId));

            try
            {
                // Retrieve detections with related entity data
                var detections = await _context.Detections
                    .Where(d => d.DeviceId == deviceId)
                    .Include(d => d.Device)
                    .Include(d => d.Media)
                    .OrderByDescending(d => d.DetectedAt)
                    .ToListAsync();

                // Log query operation with analytics
                await LogDetectionQuery(deviceId, detections);

                return detections;
            }
            catch (Exception ex)
            {
                await LogDetectionQueryFailure(deviceId, ex);
                throw;
            }
        }

        #endregion

        #region Private Methods - Detection Initialization and Validation

        /// <summary>
        /// Initializes detection with default values for required fields that aren't set.
        /// </summary>
        /// <param name="detection">The detection to initialize</param>
        /// <returns>A task representing the asynchronous initialization operation</returns>
        private async Task InitializeDetectionDefaults(Detection detection)
        {
            // Set detection time if not provided
            if (detection.DetectedAt == default)
                detection.DetectedAt = DateTime.Now;

            // Set default device ID if not provided
            if (detection.DeviceId <= 0)
                detection.DeviceId = 1; // Default camera/device ID

            await Task.CompletedTask; // Placeholder for any async initialization
        }

        /// <summary>
        /// Ensures all required foreign key relationships exist, creating default entities if necessary.
        /// </summary>
        /// <param name="detection">The detection requiring relationship validation</param>
        /// <returns>A task representing the asynchronous relationship establishment operation</returns>
        private async Task EnsureRequiredRelationships(Detection detection)
        {
            // Ensure device relationship exists
            if (detection.DeviceId <= 0)
            {
                detection.DeviceId = await GetOrCreateDefaultDevice();
            }

            // Ensure media relationship exists
            if (detection.MediaId <= 0)
            {
                var defaultMedia = await GetOrCreateDefaultMedia(detection.DeviceId);
                detection.MediaId = defaultMedia.MediaId;
            }

            // Ensure event relationship exists
            if (detection.EventId <= 0)
            {
                var defaultEvent = await GetOrCreateDefaultEvent();
                detection.EventId = defaultEvent.EventId;
            }
        }

        /// <summary>
        /// Validates that all foreign key references exist in the database.
        /// </summary>
        /// <param name="detection">The detection whose relationships to validate</param>
        /// <returns>A task representing the asynchronous validation operation</returns>
        /// <exception cref="InvalidOperationException">Thrown when any required relationship doesn't exist</exception>
        private async Task ValidateForeignKeyReferences(Detection detection)
        {
            // Validate device exists
            var deviceExists = await _context.Devices.AnyAsync(d => d.DeviceId == detection.DeviceId);
            if (!deviceExists)
                throw new InvalidOperationException($"Device with ID {detection.DeviceId} does not exist");

            // Validate media exists
            var mediaExists = await _context.Media.AnyAsync(m => m.MediaId == detection.MediaId);
            if (!mediaExists)
                throw new InvalidOperationException($"Media with ID {detection.MediaId} does not exist");

            // Validate event exists
            var eventExists = await _context.Events.AnyAsync(e => e.EventId == detection.EventId);
            if (!eventExists)
                throw new InvalidOperationException($"Event with ID {detection.EventId} does not exist");
        }

        #endregion

        #region Private Methods - Default Entity Creation

        /// <summary>
        /// Gets an existing device or creates a default device if none exists.
        /// </summary>
        /// <returns>The ID of an available device</returns>
        private async Task<int> GetOrCreateDefaultDevice()
        {
            // Try to get first available device
            var firstDevice = await _context.Devices.FirstOrDefaultAsync();
            if (firstDevice != null)
                return firstDevice.DeviceId;

            // Create a default device if none exists
            var defaultDevice = new Device
            {
                Location = "Default Camera",
                Status = "Active",
                LastActive = DateTime.Now
            };

            _context.Devices.Add(defaultDevice);
            await _context.SaveChangesAsync();
            return defaultDevice.DeviceId;
        }

        /// <summary>
        /// Gets existing media for a device or creates default media entry.
        /// </summary>
        /// <param name="deviceId">The device ID to associate with the media</param>
        /// <returns>The media entity for the device</returns>
        private async Task<Media> GetOrCreateDefaultMedia(int deviceId)
        {
            // Try to find existing default media for this device
            var existingMedia = await _context.Media
                .Where(m => m.DeviceId == deviceId)
                .FirstOrDefaultAsync();

            if (existingMedia != null)
                return existingMedia;

            // Create default media entry
            var defaultMedia = new Media
            {
                Type = "Detection",
                FilePath = $"default_detection_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                Timestamp = DateTime.Now,
                DeviceId = deviceId
            };

            _context.Media.Add(defaultMedia);
            await _context.SaveChangesAsync();
            return defaultMedia;
        }

        /// <summary>
        /// Gets an active event or creates a default event for detections.
        /// </summary>
        /// <returns>An active event entity</returns>
        private async Task<Event> GetOrCreateDefaultEvent()
        {
            // Try to find an active event
            var activeEvent = await _context.Events
                .Where(e => e.Status == "Active" || e.EndTime > DateTime.Now)
                .FirstOrDefaultAsync();

            if (activeEvent != null)
                return activeEvent;

            // Create default event
            var defaultEvent = new Event
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(DEFAULT_EVENT_DURATION_HOURS),
                Status = "Active"
            };

            _context.Events.Add(defaultEvent);
            await _context.SaveChangesAsync();
            return defaultEvent;
        }

        #endregion

        #region Private Methods - Tracking Information

        /// <summary>
        /// Populates tracking-specific information for a detection including bounding box and object details.
        /// </summary>
        /// <param name="detection">The detection to populate with tracking information</param>
        /// <param name="boundingBoxX">X coordinate of the bounding box</param>
        /// <param name="boundingBoxY">Y coordinate of the bounding box</param>
        /// <param name="boundingBoxWidth">Width of the bounding box</param>
        /// <param name="boundingBoxHeight">Height of the bounding box</param>
        /// <param name="detectedObject">Description of the detected object</param>
        /// <returns>A task representing the asynchronous population operation</returns>
        private async Task PopulateTrackingInformation(Detection detection, float boundingBoxX,
            float boundingBoxY, float boundingBoxWidth, float boundingBoxHeight, string detectedObject)
        {
            // Set bounding box coordinates
            detection.BoundingBoxX = boundingBoxX;
            detection.BoundingBoxY = boundingBoxY;
            detection.BoundingBoxWidth = boundingBoxWidth;
            detection.BoundingBoxHeight = boundingBoxHeight;
            detection.DetectedObject = detectedObject;

            // Set frame number for tracking continuity
            detection.FrameNumber = await GetNextFrameNumber(detection.DeviceId);
        }

        /// <summary>
        /// Gets the next sequential frame number for a device based on the last detection.
        /// </summary>
        /// <param name="deviceId">The device ID to get the frame number for</param>
        /// <returns>The next frame number for the device</returns>
        private async Task<int> GetNextFrameNumber(int deviceId)
        {
            var lastDetection = await _context.Detections
                .Where(d => d.DeviceId == deviceId)
                .OrderByDescending(d => d.DetectedAt)
                .FirstOrDefaultAsync();

            return (lastDetection?.FrameNumber ?? 0) + 1;
        }

        /// <summary>
        /// Initiates frame extraction for tracking analysis in a non-blocking manner.
        /// </summary>
        /// <param name="detection">The detection to extract frames for</param>
        /// <returns>A task representing the asynchronous initiation operation</returns>
        private async Task InitiateFrameExtraction(Detection detection)
        {
            // Note: In a production system, you would inject IWebHostEnvironment
            // For now, this demonstrates the integration pattern
            var mediaService = new DetectionMediaService(_context, null, _logService);

            // Run frame extraction asynchronously to avoid blocking the main thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await mediaService.ExtractFramesAsync(detection);
                }
                catch (Exception ex)
                {
                    await _logService.AddLogAsync(SYSTEM_USER_ID, "FrameExtractionFailed",
                        $"Background frame extraction failed for detection {detection.DetectionId}: {ex.Message}",
                        "Error", detection.DetectionId);
                }
            });

            await Task.CompletedTask;
        }

        private async Task TryGenerateTrackingRouteAsync(int trackingId, int deviceId)
        {
            var existingRoute = await _context.TrackingRoutes.FirstOrDefaultAsync(r => r.TrackingId == trackingId);
            if (existingRoute != null)
                return;

            var detections = await _context.Detections
                .Where(d => d.TrackingId == trackingId && d.DeviceId == deviceId)
                .OrderBy(d => d.FrameNumber)
                .ToListAsync();

            if (detections.Count < 2)
                return;

            var path = detections
                .Select(d => new float[] {
            d.BoundingBoxX + d.BoundingBoxWidth / 2,
            d.BoundingBoxY + d.BoundingBoxHeight / 2
                })
                .ToList();

            var route = new TrackingRoute
            {
                TrackingId = trackingId,
                DeviceId = deviceId,
                StartTime = detections.First().DetectedAt,
                EndTime = detections.Last().DetectedAt,
                DetectedObject = detections.First().DetectedObject,
                PathJson = JsonSerializer.Serialize(path)
            };

            _context.TrackingRoutes.Add(route);
            await _context.SaveChangesAsync();
        }


        #endregion

        #region Private Methods - Logging and Notifications

        /// <summary>
        /// Logs the creation of a detection with appropriate severity based on confidence level.
        /// </summary>
        /// <param name="detection">The detection that was created</param>
        /// <returns>A task representing the asynchronous logging operation</returns>
        private async Task LogDetectionCreation(Detection detection)
        {
            var (logLevel, actionType) = DetermineLogSeverity(detection.Confidence);

            await _logService.AddLogAsync(
                userId: SYSTEM_USER_ID,
                actionType: actionType,
                message: $"Detection created from device {detection.DeviceId} with confidence {detection.Confidence:F2}% at {detection.DetectedAt:G}",
                level: logLevel,
                detectionId: detection.DetectionId
            );
        }

        /// <summary>
        /// Determines the appropriate log level and action type based on detection confidence.
        /// </summary>
        /// <param name="confidence">The confidence level of the detection</param>
        /// <returns>A tuple containing the log level and action type</returns>
        private (string LogLevel, string ActionType) DetermineLogSeverity(double confidence)
        {
            if (confidence >= CRITICAL_CONFIDENCE_RISK)
                return ("Critical", "CriticalDetectionCreated");

            if (confidence >= MODERATE_CONFIDENCE_RISK)
                return ("Warning", "HighConfidenceDetectionCreated");

            return ("Info", "DetectionCreated");
        }

        /// <summary>
        /// Processes high-confidence detections by sending notifications and creating alerts.
        /// </summary>
        /// <param name="detection">The detection to process for high-confidence handling</param>
        /// <returns>A task representing the asynchronous processing operation</returns>
        private async Task ProcessHighConfidenceDetection(Detection detection)
        {
            if (detection.Confidence < HIGH_CONFIDENCE_RISK)
                return;

            // Log high-confidence alert
            await _logService.AddLogAsync(
                userId: SYSTEM_USER_ID,
                actionType: "HighConfidenceAlert",
                message: $"High confidence detection ({detection.Confidence:F2}%) from device {detection.DeviceId} - requires attention",
                level: "Warning",
                detectionId: detection.DetectionId
            );

            // Send notification to users
            await _notificationService.NotifyUserAsync(detection);
        }

        /// <summary>
        /// Checks for frequent detections from the same device that might indicate system issues.
        /// </summary>
        /// <param name="detection">The current detection to check against recent detections</param>
        /// <returns>A task representing the asynchronous frequency check operation</returns>
        private async Task CheckForFrequentDetections(Detection detection)
        {
            var recentDetections = await _context.Detections
                .Where(d => d.DeviceId == detection.DeviceId &&
                           d.DetectedAt >= DateTime.Now.AddMinutes(-FREQUENT_DETECTION_WINDOW_MINUTES) &&
                           d.DetectionId != detection.DetectionId)
                .CountAsync();

            if (recentDetections >= FREQUENT_DETECTION_THRESHOLD)
            {
                await _logService.AddLogAsync(
                    userId: SYSTEM_USER_ID,
                    actionType: "FrequentDetections",
                    message: $"Device {detection.DeviceId} has {recentDetections + 1} detections in last {FREQUENT_DETECTION_WINDOW_MINUTES} minutes - possible system issue or high activity",
                    level: "Warning",
                    detectionId: detection.DetectionId
                );
            }
        }

        /// <summary>
        /// Logs successful detection query operations with analytics information.
        /// </summary>
        /// <param name="deviceId">The device ID that was queried</param>
        /// <param name="detections">The detections that were retrieved</param>
        /// <returns>A task representing the asynchronous logging operation</returns>
        private async Task LogDetectionQuery(int deviceId, IEnumerable<Detection> detections)
        {
            var detectionList = detections.ToList();
            var highConfidenceCount = detectionList.Count(d => d.Confidence >= MODERATE_CONFIDENCE_RISK);

            await _logService.AddLogAsync(
                userId: SYSTEM_USER_ID,
                actionType: "DetectionsQueried",
                message: $"Retrieved {detectionList.Count} detections for device {deviceId} ({highConfidenceCount} high confidence)",
                level: "Info"
            );
        }

        /// <summary>
        /// Logs detection creation failures with detailed error information.
        /// </summary>
        /// <param name="detection">The detection that failed to be created</param>
        /// <param name="exception">The exception that occurred during creation</param>
        /// <returns>A task representing the asynchronous error logging operation</returns>
        private async Task LogDetectionCreationFailure(Detection detection, Exception exception)
        {
            await _logService.AddLogAsync(
                userId: SYSTEM_USER_ID,
                actionType: "DetectionCreationFailed",
                message: $"Failed to create detection from device {detection?.DeviceId}: {exception.Message}. Stack: {exception.StackTrace}",
                level: "Error"
            );
        }

        /// <summary>
        /// Logs detection query failures with error details.
        /// </summary>
        /// <param name="deviceId">The device ID that was being queried</param>
        /// <param name="exception">The exception that occurred during the query</param>
        /// <returns>A task representing the asynchronous error logging operation</returns>
        private async Task LogDetectionQueryFailure(int deviceId, Exception exception)
        {
            await _logService.AddLogAsync(
                userId: SYSTEM_USER_ID,
                actionType: "DetectionQueryFailed",
                message: $"Failed to retrieve detections for device {deviceId}: {exception.Message}",
                level: "Error"
            );
        }

        #endregion
    }
}
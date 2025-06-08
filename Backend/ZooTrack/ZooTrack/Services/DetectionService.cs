using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrackBackend.Services;

namespace ZooTrack.Services
{
    public class DetectionService : IDetectionService
    {
        private readonly ZootrackDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ILogService _logService;

        const double CRITICAL_CONFIDENCE_RISK = 95.0;
        const double HIGH_CONFIDENCE_RISK = 90.0;
        const double MODERATE_CONFIDENCE_RISK = 80.0;

        public DetectionService(ZootrackDbContext context, NotificationService notificationService, ILogService logService)
        {
            _context = context;
            _notificationService = notificationService;
            _logService = logService;
        }

        public async Task<Detection> CreateDetectionAsync(Detection detection)
        {
            try
            {
                // Validate detection data
                if (detection == null)
                    throw new ArgumentNullException(nameof(detection), "Detection cannot be null");

                // Set detection time if not provided
                if (detection.DetectedAt == default)
                    detection.DetectedAt = DateTime.Now;

                // Ensure DeviceId is set (you might want to get this from your camera/detection system)
                if (detection.DeviceId <= 0)
                    detection.DeviceId = 1; // Default camera/device ID

                _context.Detections.Add(detection);
                await _context.SaveChangesAsync();

                // CRITICAL FIX: Ensure all required foreign keys are set
                if (detection.DeviceId <= 0)
                {
                    // Try to get first available device or create a default one
                    var firstDevice = await _context.Devices.FirstOrDefaultAsync();
                    if (firstDevice != null)
                    {
                        detection.DeviceId = firstDevice.DeviceId;
                    }
                    else
                    {
                        // Create a default device if none exists
                        var defaultDevice = new Device
                        {
                            Location = "Default Camera",
                            Status = "Active",
                            LastActive = DateTime.Now
                        };
                        _context.Devices.Add(defaultDevice);
                        await _context.SaveChangesAsync();
                        detection.DeviceId = defaultDevice.DeviceId;
                    }
                }

                if (detection.MediaId <= 0)
                {
                    // Try to get or create default media
                    var defaultMedia = await GetOrCreateDefaultMedia(detection.DeviceId);
                    detection.MediaId = defaultMedia.MediaId;
                }

                if (detection.EventId <= 0)
                {
                    // Try to get or create default event
                    var defaultEvent = await GetOrCreateDefaultEvent();
                    detection.EventId = defaultEvent.EventId;
                }

                // Validate foreign key references exist
                var deviceExists = await _context.Devices.AnyAsync(d => d.DeviceId == detection.DeviceId);
                if (!deviceExists)
                {
                    throw new InvalidOperationException($"Device with ID {detection.DeviceId} does not exist");
                }

                var mediaExists = await _context.Media.AnyAsync(m => m.MediaId == detection.MediaId);
                if (!mediaExists)
                {
                    throw new InvalidOperationException($"Media with ID {detection.MediaId} does not exist");
                }

                var eventExists = await _context.Events.AnyAsync(e => e.EventId == detection.EventId);
                if (!eventExists)
                {
                    throw new InvalidOperationException($"Event with ID {detection.EventId} does not exist");
                }

                // Add to database
                _context.Detections.Add(detection);
                await _context.SaveChangesAsync();


                // ALWAYS log detection creation (this addresses your requirement)
                string logLevel = "Info";
                string actionType = "DetectionCreated";

                if (detection.Confidence >= CRITICAL_CONFIDENCE_RISK)
                {
                    logLevel = "Critical";
                    actionType = "CriticalDetectionCreated";
                }
                else if (detection.Confidence >= MODERATE_CONFIDENCE_RISK)
                {
                    logLevel = "Warning";
                    actionType = "HighConfidenceDetectionCreated";
                }

                // Log EVERY detection creation
                await _logService.AddLogAsync(
                    userId: 1, // System user for service-level operations
                    actionType: actionType,
                    message: $"Detection created from device {detection.DeviceId} with confidence {detection.Confidence:F2}% at {detection.DetectedAt:G}",
                    level: logLevel,
                    detectionId: detection.DetectionId
                );

                // Only send notifications/alerts for HIGH confidence detections (90%+)
                if (detection.Confidence >= HIGH_CONFIDENCE_RISK)
                {
                    await _logService.AddLogAsync(
                        userId: 1,
                        actionType: "HighConfidenceAlert",
                        message: $"High confidence detection ({detection.Confidence:F2}%) from device {detection.DeviceId} - requires attention",
                        level: "Warning",
                        detectionId: detection.DetectionId
                    );

                    // Only notify users for high confidence detections
                    await _notificationService.NotifyUserAsync(detection);
                }

                // Check for frequent detections from same device (potential issue detection)
                var recentDetections = await _context.Detections
                    .Where(d => d.DeviceId == detection.DeviceId &&
                               d.DetectedAt >= DateTime.Now.AddMinutes(-10) &&
                               d.DetectionId != detection.DetectionId)
                    .CountAsync();

                if (recentDetections >= 5)
                {
                    await _logService.AddLogAsync(
                        userId: 1,
                        actionType: "FrequentDetections",
                        message: $"Device {detection.DeviceId} has {recentDetections + 1} detections in last 10 minutes - possible system issue or high activity",
                        level: "Warning",
                        detectionId: detection.DetectionId
                    );
                }

                return detection;
            }
            catch (Exception ex)
            {
                // Log the error with detailed information
                await _logService.AddLogAsync(
                    userId: 1,
                    actionType: "DetectionCreationFailed",
                    message: $"Failed to create detection from device {detection?.DeviceId}: {ex.Message}. Stack: {ex.StackTrace}",
                    level: "Error"
                );

                throw; // Re-throw the exception to handle it upstream
            }
        }

        // HELPER METHODS TO HANDLE DEFAULT ENTITIES
        private async Task<Media> GetOrCreateDefaultMedia(int deviceId)
        {
            // Try to find existing default media for this device
            var existingMedia = await _context.Media
                .Where(m => m.DeviceId == deviceId)
                .FirstOrDefaultAsync();

            if (existingMedia != null)
                return existingMedia;

            // Create default media
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
                EndTime = DateTime.Now.AddHours(24), // 24-hour event
                Status = "Active"
            };

            _context.Events.Add(defaultEvent);
            await _context.SaveChangesAsync();
            return defaultEvent;
        }

        public async Task<IEnumerable<Detection>> GetDetectionsForDeviceAsync(int deviceId)
        {
            try
            {
                var detections = await _context.Detections
                    .Where(d => d.DeviceId == deviceId)
                    .Include(d => d.Device)
                    .Include(d => d.Media)
                    .OrderByDescending(d => d.DetectedAt)
                    .ToListAsync();

                // Log the query with analytics
                var highConfidenceCount = detections.Count(d => d.Confidence >= MODERATE_CONFIDENCE_RISK);
                await _logService.AddLogAsync(
                    userId: 1, // System user
                    actionType: "DetectionsQueried",
                    message: $"Retrieved {detections.Count} detections for device {deviceId} ({highConfidenceCount} high confidence)",
                    level: "Info"
                );

                return detections;
            }
            catch (Exception ex)
            {
                // Log the error
                await _logService.AddLogAsync(
                    userId: 1,
                    actionType: "DetectionQueryFailed",
                    message: $"Failed to retrieve detections for device {deviceId}: {ex.Message}",
                    level: "Error"
                );

                throw;
            }
        }

        public async Task<Detection> CreateDetectionWithTrackingAsync(Detection detection,
    float boundingBoxX, float boundingBoxY, float boundingBoxWidth, float boundingBoxHeight,
    string detectedObject = null)
        {
            try
            {
                // Set bounding box information
                detection.BoundingBoxX = boundingBoxX;
                detection.BoundingBoxY = boundingBoxY;
                detection.BoundingBoxWidth = boundingBoxWidth;
                detection.BoundingBoxHeight = boundingBoxHeight;
                detection.DetectedObject = detectedObject;

                // Set frame number (you might get this from your camera service)
                detection.FrameNumber = await GetCurrentFrameNumber(detection.DeviceId);

                // Create the detection using existing method
                var createdDetection = await CreateDetectionAsync(detection);

                // Extract frames for tracking (this will also handle correlation)
                var mediaService = new DetectionMediaService(_context,
                    // You'll need to inject IWebHostEnvironment into DetectionService
                    null, // Replace with actual IWebHostEnvironment 
                    _logService);

                // Note: You might want to make this async fire-and-forget to avoid blocking
                _ = Task.Run(async () => await mediaService.ExtractFramesAsync(createdDetection));

                return createdDetection;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(1, "TrackingDetectionCreationFailed",
                    $"Failed to create detection with tracking: {ex.Message}", "Error");
                throw;
            }
        }

        private async Task<int> GetCurrentFrameNumber(int deviceId)
        {
            // Get the last frame number for this device, or start at 0
            var lastDetection = await _context.Detections
                .Where(d => d.DeviceId == deviceId)
                .OrderByDescending(d => d.DetectedAt)
                .FirstOrDefaultAsync();

            return (lastDetection?.FrameNumber ?? 0) + 1;
        }

    }
}

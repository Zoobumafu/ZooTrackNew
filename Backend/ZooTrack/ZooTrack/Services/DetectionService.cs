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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrack.Services;
using ZooTrackBackend.Services;

namespace ZooTrack.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DetectionController : ControllerBase
    {
        private readonly ZootrackDbContext _context;
        // private readonly NotificationService _notificationService;
        private readonly IDetectionService _detectionService;
        private readonly ILogService _logService;


        public DetectionController(ZootrackDbContext context, IDetectionService detectionService, ILogService logService)
        {
            _context = context;
            _detectionService = detectionService;
            _logService = logService;
        }

        // GET: api/Detection
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Detection>>> GetDetections()
        {
            try
            {
                var detections = await _context.Detections
                    .Include(d => d.Device)
                    .Include(d => d.Media)
                    .OrderByDescending(d => d.DetectedAt)
                    .ToListAsync();

                // Log the query
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionsList",
                    message: $"Retrieved {detections.Count} detections",
                    level: "Info"
                );

                return detections;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionsListFailed",
                    message: $"Failed to retrieve detections: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to retrieve detections");
            }
        }

        // GET: api/Detection/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Detection>> GetDetection(int id)
        {
            try
            {
                var detection = await _context.Detections
                    .Include(d => d.Device)
                    .Include(d => d.Media)
                    .FirstOrDefaultAsync(d => d.DetectionId == id);

                if (detection == null)
                {
                    await _logService.AddLogAsync(
                        userId: GetCurrentUserId(),
                        actionType: "DetectionNotFound",
                        message: $"Detection with ID {id} not found",
                        level: "Warning"
                    );
                    return NotFound();
                }

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionViewed",
                    message: $"Detection {id} viewed",
                    level: "Info",
                    detectionId: id
                );

                return detection;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionViewFailed",
                    message: $"Failed to retrieve detection {id}: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to retrieve detection");
            }
        }

        // Get all detections for a specific tracking ID (same object across time)
        [HttpGet("Tracking/{trackingId}")]
        public async Task<ActionResult<IEnumerable<Detection>>> GetDetectionsByTrackingId(int trackingId)
        {
            try
            {
                var detections = await _context.Detections
                    .Where(d => d.TrackingId == trackingId)
                    .Include(d => d.Device)
                    .Include(d => d.Media)
                    .OrderBy(d => d.DetectedAt)
                    .ToListAsync();

                await _logService.AddLogAsync(GetCurrentUserId(), "TrackingQuery",
                    $"Retrieved {detections.Count} detections for tracking ID {trackingId}", "Info");

                return detections;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(GetCurrentUserId(), "TrackingQueryFailed",
                    $"Failed to retrieve tracking detections: {ex.Message}", "Error");
                return StatusCode(500, "Failed to retrieve tracking detections");
            }
        }

        // PUT: api/Detection/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDetection(int id, Detection detection)
        {
            if (id != detection.DetectionId)
            {
                return BadRequest();
            }

            try
            {
                var originalDetection = await _context.Detections.AsNoTracking().FirstOrDefaultAsync(d => d.DetectionId == id);

                _context.Entry(detection).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Log the detection update with details about what changed
                var changes = new List<string>();
                if (originalDetection != null)
                {
                    if (Math.Abs(originalDetection.Confidence - detection.Confidence) > 0.01)
                        changes.Add($"Confidence: {originalDetection.Confidence:F2}% -> {detection.Confidence:F2}%");
                    if (originalDetection.DeviceId != detection.DeviceId)
                        changes.Add($"Device: {originalDetection.DeviceId} -> {detection.DeviceId}");
                }

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionUpdated",
                    message: $"Detection {detection.DetectionId} updated. Changes: {string.Join(", ", changes)}",
                    level: "Info",
                    detectionId: detection.DetectionId
                );
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DetectionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionUpdateFailed",
                    message: $"Failed to update detection {id}: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to update detection");
            }

            return NoContent();
        }

        // POST: api/Detection
        [HttpPost]
        public async Task<ActionResult<Detection>> PostDetection(Detection detection)
        {
            try
            {
                // Validate the detection data
                if (detection == null)
                {
                    return BadRequest("Detection data is required");
                }

                if (detection.Confidence < 0 || detection.Confidence > 100)
                {
                    return BadRequest("Confidence must be between 0 and 100");
                }

                // CRITICAL: Check required foreign keys
                if (detection.DeviceId <= 0)
                {
                    return BadRequest("Valid DeviceId is required");
                }

                if (detection.MediaId <= 0)
                {
                    return BadRequest("Valid MediaId is required");
                }

                if (detection.EventId <= 0)
                {
                    return BadRequest("Valid EventId is required");
                }

                // Verify foreign key references exist
                var deviceExists = await _context.Devices.AnyAsync(d => d.DeviceId == detection.DeviceId);
                if (!deviceExists)
                {
                    return BadRequest($"Device with ID {detection.DeviceId} does not exist");
                }

                var mediaExists = await _context.Media.AnyAsync(m => m.MediaId == detection.MediaId);
                if (!mediaExists)
                {
                    return BadRequest($"Media with ID {detection.MediaId} does not exist");
                }

                var eventExists = await _context.Events.AnyAsync(e => e.EventId == detection.EventId);
                if (!eventExists)
                {
                    return BadRequest($"Event with ID {detection.EventId} does not exist");
                }

                // Set default values if not provided
                if (detection.DetectedAt == default(DateTime))
                {
                    detection.DetectedAt = DateTime.Now;
                }

                // Log the detection data being processed
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionProcessing",
                    message: $"Processing detection: DeviceId={detection.DeviceId}, MediaId={detection.MediaId}, EventId={detection.EventId}, Confidence={detection.Confidence}",
                    level: "Info"
                );

                // Use the DetectionService
                var createdDetection = await _detectionService.CreateDetectionAsync(detection);

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionCreated",
                    message: $"Detection {createdDetection.DetectionId} created successfully",
                    level: "Info",
                    detectionId: createdDetection.DetectionId
                );

                return CreatedAtAction(nameof(GetDetection), new { id = createdDetection.DetectionId }, createdDetection);
            }
            catch (DbUpdateException dbEx)
            {
                // Database-specific errors (foreign key constraints, etc.)
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionDbError",
                    message: $"Database error creating detection: {dbEx.InnerException?.Message ?? dbEx.Message}",
                    level: "Error"
                );

                return StatusCode(500, $"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionControllerError",
                    message: $"Controller failed to process detection: {ex.Message}",
                    level: "Error"
                );

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Endpoint for creating detections with tracking data
        [HttpPost("WithTracking")]
        public async Task<ActionResult<Detection>> PostDetectionWithTracking([FromBody] ZooTrackBackend.Models.DetectionWithTrackingRequest request)
        {
            try
            {
                var detection = new Detection
                {
                    Confidence = request.Confidence,
                    DeviceId = request.DeviceId,
                    MediaId = request.MediaId,
                    EventId = request.EventId,
                    DetectedAt = request.DetectedAt ?? DateTime.Now
                };

                var createdDetection = await _detectionService.CreateDetectionWithTrackingAsync(
                    detection,
                    request.BoundingBoxX,
                    request.BoundingBoxY,
                    request.BoundingBoxWidth,
                    request.BoundingBoxHeight,
                    request.DetectedObject
                );

                return CreatedAtAction(nameof(GetDetection), new { id = createdDetection.DetectionId }, createdDetection);
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(GetCurrentUserId(), "TrackingDetectionFailed",
                    $"Failed to create detection with tracking: {ex.Message}", "Error");
                return StatusCode(500, "Failed to create detection with tracking");
            }
        }

        // DELETE: api/Detection/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDetection(int id)
        {
            try
            {
                var detection = await _context.Detections.FindAsync(id);
                if (detection == null)
                {
                    await _logService.AddLogAsync(
                        userId: GetCurrentUserId(),
                        actionType: "DetectionDeleteFailed",
                        message: $"Attempted to delete non-existent detection {id}",
                        level: "Warning"
                    );
                    return NotFound();
                }

                // Store info before deletion for logging
                var deviceId = detection.DeviceId;
                var confidence = detection.Confidence;
                var detectedAt = detection.DetectedAt;

                _context.Detections.Remove(detection);
                await _context.SaveChangesAsync();

                // Log the deletion
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionDeleted",
                    message: $"Detection {id} deleted (was from device {deviceId}, {confidence:F2}% confidence, detected at {detectedAt:G})",
                    level: "Info"
                );

                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionDeleteFailed",
                    message: $"Failed to delete detection {id}: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to delete detection");
            }
        }

        // GET: api/Detection/Device/5
        [HttpGet("Device/{deviceId}")]
        public async Task<ActionResult<IEnumerable<Detection>>> GetDetectionsForDevice(int deviceId)
        {
            try
            {
                // Use the service method instead
                var detections = await _detectionService.GetDetectionsForDeviceAsync(deviceId);
                return Ok(detections);
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DeviceDetectionsQueryFailed",
                    message: $"Failed to retrieve detections for device {deviceId}: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to retrieve device detections");
            }
        }

        // GET: api/Detection/Recent
        [HttpGet("Recent")]
        public async Task<ActionResult<IEnumerable<Detection>>> GetRecentDetections([FromQuery] int hours = 24)
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-hours);
                var detections = await _context.Detections
                    .Where(d => d.DetectedAt >= cutoffTime)
                    .Include(d => d.Device)
                    .OrderByDescending(d => d.DetectedAt)
                    .ToListAsync();

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "RecentDetectionsQueried",
                    message: $"Retrieved {detections.Count} detections from last {hours} hours",
                    level: "Info"
                );

                return detections;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "RecentDetectionsQueryFailed",
                    message: $"Failed to retrieve recent detections: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to retrieve recent detections");
            }
        }


        private bool DetectionExists(int id)
        {
            return _context.Detections.Any(e => e.DetectionId == id);
        }

        // Helper method to get current user ID
        private int GetCurrentUserId()
        {
            // Try to get user ID from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value
                              ?? User.FindFirst("sub")?.Value; // 'sub' is standard for JWT

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            // For API calls without authentication, use system user
            // You might want to create a dedicated system user in your database
            return 1; // System/API user ID
        }
    }
}

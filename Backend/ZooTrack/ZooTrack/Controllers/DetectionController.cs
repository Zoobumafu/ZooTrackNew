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
        private readonly NotificationService _notificationService;
        private readonly ILogService _logService;


        public DetectionController(ZootrackDbContext context, NotificationService notificationService, ILogService logService)
        {
            _context = context;
            _notificationService = notificationService;
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
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Detection>> PostDetection(Detection detection)
        {
            try
            {
                // Set the detection time if not provided
                if (detection.DetectedAt == default)
                    detection.DetectedAt = DateTime.Now;

                _context.Detections.Add(detection);
                await _context.SaveChangesAsync();

                // Determine log level based on confidence
                string logLevel = "Info";
                if (detection.Confidence >= 95.0) logLevel = "Critical";
                else if (detection.Confidence >= 80.0) logLevel = "Warning";

                // Log the positive detection
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionCreated",
                    message: $"New detection from device {detection.DeviceId} with {detection.Confidence:F2}% confidence at {detection.DetectedAt:G}",
                    level: logLevel,
                    detectionId: detection.DetectionId
                );

                // Log additional info for high confidence detections
                if (detection.Confidence >= 90.0)
                {
                    await _logService.AddLogAsync(
                        userId: GetCurrentUserId(),
                        actionType: "HighConfidenceDetection",
                        message: $"High confidence detection alert: {detection.Confidence:F2}% from device {detection.DeviceId}",
                        level: "Warning",
                        detectionId: detection.DetectionId
                    );
                }

                // Send notifications
                await _notificationService.NotifyUserAsync(detection);

                return CreatedAtAction(nameof(GetDetection), new { id = detection.DetectionId }, detection);
            }
            catch (Exception ex)
            {
                // Log the error
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionCreationFailed",
                    message: $"Failed to create detection: {ex.Message}",
                    level: "Error"
                );

                return StatusCode(500, "Internal server error occurred while creating detection");
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
                var detections = await _context.Detections
                    .Where(d => d.DeviceId == deviceId)
                    .Include(d => d.Device)
                    .Include(d => d.Media)
                    .OrderByDescending(d => d.DetectedAt)
                    .ToListAsync();

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DeviceDetectionsQueried",
                    message: $"Retrieved {detections.Count} detections for device {deviceId}",
                    level: "Info"
                );

                return detections;
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

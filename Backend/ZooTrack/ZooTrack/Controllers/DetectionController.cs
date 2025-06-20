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
        private readonly IDetectionService _detectionService;
        private readonly ILogService _logService;

        public DetectionController(ZootrackDbContext context, IDetectionService detectionService, ILogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

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

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "TrackingQuery",
                    message: $"Retrieved {detections.Count} detections for tracking ID {trackingId}",
                    level: "Info"
                );

                return detections;
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "TrackingQueryFailed",
                    message: $"Failed to retrieve tracking detections: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to retrieve tracking detections");
            }
        }

        [HttpGet("Device/{deviceId}")]
        public async Task<ActionResult<IEnumerable<Detection>>> GetDetectionsForDevice(int deviceId)
        {
            try
            {
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

        [HttpPost]
        public async Task<ActionResult<Detection>> PostDetection(Detection detection)
        {
            try
            {
                var validationResult = await ValidateDetectionAsync(detection);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                if (detection.DetectedAt == default(DateTime))
                {
                    detection.DetectedAt = DateTime.Now;
                }

                var createdDetection = await _detectionService.CreateDetectionAsync(detection);

                return CreatedAtAction(nameof(GetDetection), new { id = createdDetection.DetectionId }, createdDetection);
            }
            catch (DbUpdateException dbEx)
            {
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

        [HttpPost("WithTracking")]
        public async Task<ActionResult<Detection>> PostDetectionWithTracking([FromBody] DetectionWithTrackingRequest request)
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
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "TrackingDetectionFailed",
                    message: $"Failed to create detection with tracking: {ex.Message}",
                    level: "Error"
                );
                return StatusCode(500, "Failed to create detection with tracking");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutDetection(int id, Detection detection)
        {
            if (id != detection.DetectionId)
            {
                return BadRequest("Detection ID mismatch");
            }

            try
            {
                var originalDetection = await _context.Detections.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DetectionId == id);

                if (originalDetection == null)
                {
                    return NotFound();
                }

                _context.Entry(detection).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                var changes = GetDetectionChanges(originalDetection, detection);
                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionUpdated",
                    message: $"Detection {detection.DetectionId} updated. Changes: {string.Join(", ", changes)}",
                    level: "Info",
                    detectionId: detection.DetectionId
                );

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DetectionExists(id)) return NotFound();
                else throw;
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
        }

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

                _context.Detections.Remove(detection);
                await _context.SaveChangesAsync();

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "DetectionDeleted",
                    message: $"Detection {id} deleted",
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

        private async Task<ValidationResult> ValidateDetectionAsync(Detection detection)
        {
            if (detection == null) return new ValidationResult(false, "Detection data is required");
            if (detection.Confidence < 0 || detection.Confidence > 100) return new ValidationResult(false, "Confidence must be between 0 and 100");
            if (detection.DeviceId <= 0) return new ValidationResult(false, "Valid DeviceId is required");
            if (detection.MediaId <= 0) return new ValidationResult(false, "Valid MediaId is required");
            if (detection.EventId <= 0) return new ValidationResult(false, "Valid EventId is required");

            if (!await _context.Devices.AnyAsync(d => d.DeviceId == detection.DeviceId)) return new ValidationResult(false, $"Device with ID {detection.DeviceId} does not exist");
            if (!await _context.Media.AnyAsync(m => m.MediaId == detection.MediaId)) return new ValidationResult(false, $"Media with ID {detection.MediaId} does not exist");
            if (!await _context.Events.AnyAsync(e => e.EventId == detection.EventId)) return new ValidationResult(false, $"Event with ID {detection.EventId} does not exist");

            return new ValidationResult(true, null);
        }

        private static List<string> GetDetectionChanges(Detection original, Detection updated)
        {
            var changes = new List<string>();
            if (Math.Abs(original.Confidence - updated.Confidence) > 0.01) changes.Add($"Confidence: {original.Confidence:F2}% -> {updated.Confidence:F2}%");
            if (original.DeviceId != updated.DeviceId) changes.Add($"Device: {original.DeviceId} -> {updated.DeviceId}");
            if (original.MediaId != updated.MediaId) changes.Add($"Media: {original.MediaId} -> {updated.MediaId}");
            if (original.EventId != updated.EventId) changes.Add($"Event: {original.EventId} -> {updated.EventId}");
            if (original.TrackingId != updated.TrackingId) changes.Add($"Tracking: {original.TrackingId} -> {updated.TrackingId}");
            if (changes.Count == 0) changes.Add("No significant changes detected");
            return changes;
        }

        private bool DetectionExists(int id) => _context.Detections.Any(e => e.DetectionId == id);

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 1; // System/API user ID
        }

        private class ValidationResult
        {
            public bool IsValid { get; }
            public string ErrorMessage { get; }
            public ValidationResult(bool isValid, string errorMessage) { IsValid = isValid; ErrorMessage = errorMessage; }
        }
    }
}

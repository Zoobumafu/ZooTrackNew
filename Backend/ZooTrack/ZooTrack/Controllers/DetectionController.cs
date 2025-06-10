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
    /// <summary>
    /// Controller responsible for managing detection operations in the ZooTrack system.
    /// Handles CRUD operations for detections, tracking queries, and device-specific detection retrieval.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DetectionController : ControllerBase
    {
        #region Private Fields

        private readonly ZootrackDbContext _context;
        private readonly IDetectionService _detectionService;
        private readonly ILogService _logService;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DetectionController.
        /// </summary>
        /// <param name="context">The database context for ZooTrack operations</param>
        /// <param name="detectionService">Service for detection-related business logic</param>
        /// <param name="logService">Service for logging application events</param>
        public DetectionController(ZootrackDbContext context, IDetectionService detectionService, ILogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        #endregion

        #region GET Endpoints

        /// <summary>
        /// Retrieves all detections from the database.
        /// </summary>
        /// <returns>A list of all detections with associated device and media information, ordered by detection time (newest first)</returns>
        /// <response code="200">Returns the list of detections</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Detection>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>
        /// Retrieves a specific detection by its ID.
        /// </summary>
        /// <param name="id">The unique identifier of the detection</param>
        /// <returns>The detection with the specified ID, including device and media information</returns>
        /// <response code="200">Returns the requested detection</response>
        /// <response code="404">If the detection with the specified ID is not found</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Detection), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>
        /// Retrieves all detections associated with a specific tracking ID.
        /// This endpoint returns all detections of the same object across different time periods.
        /// </summary>
        /// <param name="trackingId">The tracking identifier that groups related detections</param>
        /// <returns>A chronologically ordered list of detections for the specified tracking ID</returns>
        /// <response code="200">Returns the list of detections for the tracking ID</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("Tracking/{trackingId}")]
        [ProducesResponseType(typeof(IEnumerable<Detection>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>
        /// Retrieves all detections recorded by a specific device.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device</param>
        /// <returns>A list of all detections from the specified device</returns>
        /// <response code="200">Returns the list of detections from the device</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("Device/{deviceId}")]
        [ProducesResponseType(typeof(IEnumerable<Detection>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>
        /// Retrieves recent detections within a specified time window.
        /// </summary>
        /// <param name="hours">Number of hours to look back from current time (default: 24 hours)</param>
        /// <returns>A list of detections from the specified time period, ordered by detection time (newest first)</returns>
        /// <response code="200">Returns the list of recent detections</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("Recent")]
        [ProducesResponseType(typeof(IEnumerable<Detection>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        #endregion

        #region POST Endpoints

        /// <summary>
        /// Creates a new detection record in the database.
        /// Validates all required fields and foreign key references before creation.
        /// </summary>
        /// <param name="detection">The detection object to create</param>
        /// <returns>The created detection with assigned ID</returns>
        /// <response code="201">Returns the newly created detection</response>
        /// <response code="400">If the detection data is invalid or required fields are missing</response>
        /// <response code="500">If there was an internal server error or database constraint violation</response>
        [HttpPost]
        [ProducesResponseType(typeof(Detection), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Detection>> PostDetection(Detection detection)
        {
            try
            {
                // Validate the detection data
                var validationResult = await ValidateDetectionAsync(detection);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
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

        /// <summary>
        /// Creates a new detection record with associated tracking information.
        /// This endpoint handles detections that include bounding box coordinates and object tracking data.
        /// </summary>
        /// <param name="request">The detection request containing both detection and tracking information</param>
        /// <returns>The created detection with tracking data</returns>
        /// <response code="201">Returns the newly created detection with tracking information</response>
        /// <response code="400">If the request data is invalid</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpPost("WithTracking")]
        [ProducesResponseType(typeof(Detection), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

                await _logService.AddLogAsync(
                    userId: GetCurrentUserId(),
                    actionType: "TrackingDetectionCreated",
                    message: $"Detection with tracking created: ID {createdDetection.DetectionId}",
                    level: "Info",
                    detectionId: createdDetection.DetectionId
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

        #endregion

        #region PUT Endpoints

        /// <summary>
        /// Updates an existing detection record.
        /// Tracks changes made to the detection and logs them for audit purposes.
        /// </summary>
        /// <param name="id">The ID of the detection to update</param>
        /// <param name="detection">The updated detection data</param>
        /// <returns>No content on successful update</returns>
        /// <response code="204">If the detection was successfully updated</response>
        /// <response code="400">If the ID in the URL doesn't match the detection ID</response>
        /// <response code="404">If the detection with the specified ID is not found</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

                // Log the detection update with details about what changed
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
        }

        #endregion

        #region DELETE Endpoints

        /// <summary>
        /// Deletes a detection record from the database.
        /// Logs the deletion with details about the removed detection for audit purposes.
        /// </summary>
        /// <param name="id">The ID of the detection to delete</param>
        /// <returns>No content on successful deletion</returns>
        /// <response code="204">If the detection was successfully deleted</response>
        /// <response code="404">If the detection with the specified ID is not found</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Validates a detection object before creation or update.
        /// Checks required fields, data ranges, and foreign key references.
        /// </summary>
        /// <param name="detection">The detection object to validate</param>
        /// <returns>A validation result indicating success or failure with error message</returns>
        private async Task<ValidationResult> ValidateDetectionAsync(Detection detection)
        {
            if (detection == null)
            {
                return new ValidationResult(false, "Detection data is required");
            }

            if (detection.Confidence < 0 || detection.Confidence > 100)
            {
                return new ValidationResult(false, "Confidence must be between 0 and 100");
            }

            if (detection.DeviceId <= 0)
            {
                return new ValidationResult(false, "Valid DeviceId is required");
            }

            if (detection.MediaId <= 0)
            {
                return new ValidationResult(false, "Valid MediaId is required");
            }

            if (detection.EventId <= 0)
            {
                return new ValidationResult(false, "Valid EventId is required");
            }

            // Verify foreign key references exist
            var deviceExists = await _context.Devices.AnyAsync(d => d.DeviceId == detection.DeviceId);
            if (!deviceExists)
            {
                return new ValidationResult(false, $"Device with ID {detection.DeviceId} does not exist");
            }

            var mediaExists = await _context.Media.AnyAsync(m => m.MediaId == detection.MediaId);
            if (!mediaExists)
            {
                return new ValidationResult(false, $"Media with ID {detection.MediaId} does not exist");
            }

            var eventExists = await _context.Events.AnyAsync(e => e.EventId == detection.EventId);
            if (!eventExists)
            {
                return new ValidationResult(false, $"Event with ID {detection.EventId} does not exist");
            }

            return new ValidationResult(true, null);
        }

        /// <summary>
        /// Compares two detection objects and returns a list of changes.
        /// Used for audit logging when detections are updated.
        /// </summary>
        /// <param name="original">The original detection before changes</param>
        /// <param name="updated">The updated detection after changes</param>
        /// <returns>A list of strings describing the changes made</returns>
        private static List<string> GetDetectionChanges(Detection original, Detection updated)
        {
            var changes = new List<string>();

            if (Math.Abs(original.Confidence - updated.Confidence) > 0.01)
                changes.Add($"Confidence: {original.Confidence:F2}% -> {updated.Confidence:F2}%");

            if (original.DeviceId != updated.DeviceId)
                changes.Add($"Device: {original.DeviceId} -> {updated.DeviceId}");

            if (original.MediaId != updated.MediaId)
                changes.Add($"Media: {original.MediaId} -> {updated.MediaId}");

            if (original.EventId != updated.EventId)
                changes.Add($"Event: {original.EventId} -> {updated.EventId}");

            if (original.TrackingId != updated.TrackingId)
                changes.Add($"Tracking: {original.TrackingId} -> {updated.TrackingId}");

            if (changes.Count == 0)
                changes.Add("No significant changes detected");

            return changes;
        }

        /// <summary>
        /// Checks if a detection with the specified ID exists in the database.
        /// </summary>
        /// <param name="id">The detection ID to check</param>
        /// <returns>True if the detection exists, false otherwise</returns>
        private bool DetectionExists(int id)
        {
            return _context.Detections.Any(e => e.DetectionId == id);
        }

        /// <summary>
        /// Retrieves the current user ID from JWT claims or returns a default system user ID.
        /// Handles various claim types commonly used in JWT tokens.
        /// </summary>
        /// <returns>The current user ID, or 1 (system user) if no authenticated user is found</returns>
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

        #endregion

        /// <summary>
        /// Represents the result of a validation operation.
        /// </summary>
        private class ValidationResult
        {
            public bool IsValid { get; }
            public string ErrorMessage { get; }

            public ValidationResult(bool isValid, string errorMessage)
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }
        }
    }
}
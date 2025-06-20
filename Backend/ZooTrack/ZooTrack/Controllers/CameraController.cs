/* * About this controller:
 * 1. This controller acts as interface between client app and camera processing service.
 * 2. It now supports discovering connected cameras and managing multiple camera streams.
 * 3. It allows:
 * a. discovering all available system cameras
 * b. start recording and processing video for specific cameras
 * c. stop the recording process for specific cameras or all cameras
 * d. check the status of the camera system
 * */


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq; // Needed for Linq Select
using System.Threading.Tasks;
using ZooTrack.Data;
using ZooTrack.Services;

namespace ZooTrack
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly ILogger<CameraController> _logger;
        private readonly CameraService _cameraService;
        private readonly ZootrackDbContext _context;


        public CameraController(ILogger<CameraController> logger, CameraService cameraService, ZootrackDbContext context)
        {
            _logger = logger;
            _cameraService = cameraService;
            _context = context;
        }

        // Class to define the expected JSON body for the start request
        public class StartRequest
        {
            public List<int> CameraIds { get; set; } = new List<int>();
            public List<string>? TargetAnimals { get; set; }
            public string? HighlightSavePath { get; set; }
        }

        public class StopRequest
        {
            // If empty, stop all. Otherwise, stop specified.
            public List<int> CameraIds { get; set; } = new List<int>();
        }

        public class CameraInfo
        {
            public int CameraId { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
        }

        [HttpGet("discover")]
        public IActionResult DiscoverCameras()
        {
            try
            {
                _logger.LogInformation("API: Received request to discover cameras.");
                var cameras = _cameraService.DiscoverCameras();
                var cameraInfos = cameras.Select(cam => new CameraInfo { CameraId = cam.CameraId, Name = cam.Name, IsActive = cam.IsActive }).ToList();
                _logger.LogInformation("Discovered {Count} cameras.", cameraInfos.Count);
                return Ok(cameraInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering cameras.");
                return StatusCode(500, new { message = "An error occurred while discovering cameras." });
            }
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartProcessing([FromBody] StartRequest request)
        {
            try
            {
                if (request == null || !request.CameraIds.Any())
                {
                    return BadRequest(new { message = "Request body is required and must contain at least one CameraId." });
                }

                // For simplicity, we use a single user's settings for this batch operation.
                // A more complex system might fetch settings per camera or user.
                var userSettings = await _context.UserSettings.FirstOrDefaultAsync(us => us.UserId == 1); // Default to system user
                if (userSettings == null)
                {
                    return NotFound(new { message = "Default user settings not found." });
                }

                var targetAnimals = request.TargetAnimals?.Any() == true ? request.TargetAnimals : userSettings.TargetAnimals;
                var highlightSavePath = !string.IsNullOrWhiteSpace(request.HighlightSavePath) ? request.HighlightSavePath : userSettings.HighlightSavePath;

                if (string.IsNullOrWhiteSpace(highlightSavePath))
                {
                    return BadRequest(new { message = "HighlightSavePath is required." });
                }

                Directory.CreateDirectory(highlightSavePath);

                var targetAnimalsLower = targetAnimals?.Select(a => a.ToLowerInvariant()).ToList() ?? new List<string>();

                _logger.LogInformation("API: Received request to start processing for CameraIds: {Ids}", string.Join(", ", request.CameraIds));

                foreach (var cameraId in request.CameraIds)
                {
                    if (!_cameraService.IsCameraInitialized(cameraId))
                    {
                        if (!_cameraService.InitializeCamera(cameraId))
                        {
                            _logger.LogError("API: Failed to initialize CameraId: {CameraId}", cameraId);
                            // Decide whether to continue with other cameras or fail the whole request
                            continue; // Continue to next camera
                        }
                    }
                    _cameraService.StartProcessing(cameraId, targetAnimalsLower, highlightSavePath);
                }

                return Ok(new { message = $"Processing started for cameras: {string.Join(", ", request.CameraIds)}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting camera processing.");
                return StatusCode(500, new { message = "Internal server error occurred while starting camera processing." });
            }
        }

        [HttpPost("stop")]
        public IActionResult StopProcessing([FromBody] StopRequest request)
        {
            try
            {
                if (request.CameraIds != null && request.CameraIds.Any())
                {
                    _logger.LogInformation("API: Received request to stop processing for CameraIds: {Ids}", string.Join(", ", request.CameraIds));
                    foreach (var cameraId in request.CameraIds)
                    {
                        _cameraService.StopProcessing(cameraId);
                    }
                    return Ok(new { message = $"Processing stopped for cameras: {string.Join(", ", request.CameraIds)}." });
                }
                else
                {
                    _logger.LogInformation("API: Received request to stop all processing.");
                    _cameraService.StopAllProcessing();
                    return Ok(new { message = "All camera processing stopped successfully." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping camera processing.");
                return StatusCode(500, new { message = "Internal server error occurred while stopping camera processing." });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            try
            {
                var statuses = _cameraService.GetAllStatuses();
                return Ok(statuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting camera status.");
                return StatusCode(500, new { message = "Internal server error occurred while getting camera status." });
            }
        }
    }
}
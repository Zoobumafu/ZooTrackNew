/* 
 * About this controller:
 *  1. This controller acts as interface between client app and camera processing service.
 *  2. It allows:
 *      a. start recording and processing video when detection is positive
 *      b. stop the recording process
 *      c. check the status of the camera system
 *      
 */


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq; // Needed for Linq Select
using ZooTrack.Data;
using ZooTrack.Services;

namespace ZooTrack.Controllers
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
            public int UserId { get; set; } = 1; // Default to system user
            public List<string>? TargetAnimals { get; set; } // Optional - will use user settings if not provided
            public string? HighlightSavePath { get; set; } // Optional - will use user settings if not provided
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartProcessing([FromBody] StartRequest request)
        {
            try
            {
                // Validate the request body
                if (request == null)
                {
                    _logger.LogWarning("Start request failed: Invalid request body.");
                    return BadRequest(new { message = "Request body is required." });
                }

                // Get user settings from database
                var userSettings = await _context.UserSettings.FirstOrDefaultAsync(us => us.UserId == request.UserId);

                if (userSettings == null)
                {
                    _logger.LogWarning($"User settings not found for UserId: {request.UserId}");
                    return NotFound(new { message = $"User settings not found for UserId: {request.UserId}" });
                }

                // Use provided values or fall back to user settings
                var targetAnimals = request.TargetAnimals?.Any() == true
                    ? request.TargetAnimals
                    : userSettings.TargetAnimals;

                var highlightSavePath = !string.IsNullOrWhiteSpace(request.HighlightSavePath)
                    ? request.HighlightSavePath
                    : userSettings.HighlightSavePath;

                if (string.IsNullOrWhiteSpace(highlightSavePath))
                {
                    _logger.LogWarning("Start request failed: No highlight save path available.");
                    return BadRequest(new { message = "HighlightSavePath is required in user settings or request." });
                }

                // Ensure the highlight save directory exists
                try
                {
                    Directory.CreateDirectory(highlightSavePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to create highlight save directory: {ex.Message}");
                    return StatusCode(500, new { message = "Failed to create highlight save directory." });
                }

                // Convert animal names to lower case for consistent matching
                var targetAnimalsLower = targetAnimals?.Select(a => a.ToLowerInvariant()).ToList() ?? new List<string>();

                _logger.LogInformation("API: Received request to start processing for UserId: {UserId}. Targets: {Targets}, Path: {Path}",
                    request.UserId, string.Join(",", targetAnimalsLower), highlightSavePath);

                // Initialize CameraService if it hasn't been initialized yet
                if (!_cameraService.IsInitialized)
                {
                    _logger.LogInformation("API: Initializing CameraService on demand...");
                    if (!_cameraService.InitializeCameraAndYolo())
                    {
                        _logger.LogError("API: Initialization failed via start request.");
                        return StatusCode(500, new { message = "Failed to initialize camera or YOLO model." });
                    }
                }

                // Set the target animals and save path in the CameraService
                _cameraService.SetProcessingTargets(targetAnimalsLower, highlightSavePath);

                // Signal the CameraService to start processing
                _cameraService.StartProcessing();

                // Update user settings if they were modified via the request
                if (request.TargetAnimals?.Any() == true)
                {
                    userSettings.TargetAnimals = request.TargetAnimals;
                    await _context.SaveChangesAsync();

                    // Also save to TargetAnimals.json file
                    string targetAnimalsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TargetAnimals.json");
                    userSettings.SaveTargetAnimalsToFile(targetAnimalsFilePath);
                }

                return Ok(new
                {
                    message = "Camera processing started successfully.",
                    userId = request.UserId,
                    targetAnimals = targetAnimalsLower,
                    highlightSavePath = highlightSavePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting camera processing: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error occurred while starting camera processing." });
            }
        }

         [HttpPost("stop")]
        public IActionResult StopProcessing()
        {
            try
            {
                _logger.LogInformation("API: Received request to stop processing.");
                
                _cameraService.StopProcessing();
                
                return Ok(new { message = "Camera processing stopped successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping camera processing: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error occurred while stopping camera processing." });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            try
            {
                var status = new
                {
                    IsInitialized = _cameraService.IsInitialized,
                    IsProcessing = _cameraService.IsProcessing,
                    // Add any other relevant status information
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting camera status: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error occurred while getting camera status." });
            }
        }

        [HttpPut("settings/{userId}")]
        public async Task<IActionResult> UpdateUserCameraSettings(int userId, [FromBody] UpdateSettingsRequest request)
        {
            try
            {
                var userSettings = await _context.UserSettings.FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = $"User settings not found for UserId: {userId}" });
                }

                // Update settings
                if (request.TargetAnimals != null)
                {
                    userSettings.TargetAnimals = request.TargetAnimals;

                    // Save to TargetAnimals.json file
                    string targetAnimalsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TargetAnimals.json");
                    userSettings.SaveTargetAnimalsToFile(targetAnimalsFilePath);
                }

                if (!string.IsNullOrWhiteSpace(request.HighlightSavePath))
                {
                    userSettings.HighlightSavePath = request.HighlightSavePath;
                }

                if (request.DetectionThreshold.HasValue)
                {
                    userSettings.DetectionThreshold = request.DetectionThreshold.Value;
                }

                if (!string.IsNullOrWhiteSpace(request.NotificationPreference))
                {
                    userSettings.NotificationPreference = request.NotificationPreference;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "User settings updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating user camera settings: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error occurred while updating user settings." });
            }
        }

        public class UpdateSettingsRequest
        {
            public List<string>? TargetAnimals { get; set; }
            public string? HighlightSavePath { get; set; }
            public float? DetectionThreshold { get; set; }
            public string? NotificationPreference { get; set; }
        }

    }
}
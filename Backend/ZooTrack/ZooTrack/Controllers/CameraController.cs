// ZooTrack.WebAPI/Controllers/CameraController.cs
// Make sure the namespace matches your project structure, e.g., ZooTrack.Controllers or ZooTrackBackend.Controllers

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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq; // Needed for Linq Select
using ZooTrack.Services; // Assuming CameraService is in ZooTrack.Services

namespace ZooTrack.Controllers // <-- Make sure this namespace is correct for your project
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly ILogger<CameraController> _logger;
        private readonly CameraService _cameraService;
        // No direct reference to the Background Service needed here,
        // we interact via the shared CameraService state.

        public CameraController(ILogger<CameraController> logger, CameraService cameraService)
        {
            _logger = logger;
            _cameraService = cameraService;
        }

        // Class to define the expected JSON body for the start request
        public class StartRequest
        {
            public List<string> TargetAnimals { get; set; } = new List<string>();
            public string HighlightSavePath { get; set; } = string.Empty;
        }

        [HttpPost("start")]
        public IActionResult StartProcessing([FromBody] StartRequest request)
        {
            // Validate the request body
            if (request == null || string.IsNullOrWhiteSpace(request.HighlightSavePath))
            {
                _logger.LogWarning("Start request failed: Invalid request body or missing save path.");
                // Provide a clear error message to the client
                return BadRequest(new { message = "HighlightSavePath is required. Provide TargetAnimals list (can be empty)." });
            }

            // Convert animal names to lower case for consistent matching inside CameraService
            var targetAnimalsLower = request.TargetAnimals?.Select(a => a.ToLowerInvariant()).ToList() ?? new List<string>();

            _logger.LogInformation("API: Received request to start processing. Targets: {Targets}, Path: {Path}", string.Join(",", targetAnimalsLower), request.HighlightSavePath);

            // Initialize CameraService if it hasn't been initialized yet (e.g., on first request)
            // This provides lazy initialization.
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
            _cameraService.SetProcessingTargets(targetAnimalsLower, request.HighlightSavePath);

            // Signal the CameraService (and implicitly the background service) to start the processing loop
            _cameraService.StartProcessing(); // Sets the IsProcessing flag checked by CameraProcessingService

            return Ok(new { message = "Camera processing signaled to start." });
        }

        [HttpPost("stop")]
        public IActionResult StopProcessing()
        {
            _logger.LogInformation("API: Received request to stop processing.");
            // Signal the CameraService (and implicitly the background service) to stop the processing loop
            _cameraService.StopProcessing(); // Sets the IsProcessing flag to false
            return Ok(new { message = "Camera processing signaled to stop." });
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            _logger.LogDebug("API: Received request for status."); // Use Debug level for frequent status checks
            // Return the current status based on the CameraService state
            return Ok(new
            {
                IsInitialized = _cameraService.IsInitialized,
                IsProcessing = _cameraService.IsProcessing
            });
        }
    }
}
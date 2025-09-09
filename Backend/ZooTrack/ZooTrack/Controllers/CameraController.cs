// REWRITTEN - SECURITY REMOVED
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ZooTrack.Services;

namespace ZooTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] // <-- THIS LINE HAS BEEN REMOVED
    public class CameraController : ControllerBase
    {
        private readonly ILogger<CameraController> _logger;
        private readonly CameraService _cameraService;

        public CameraController(ILogger<CameraController> logger, CameraService cameraService)
        {
            _logger = logger;
            _cameraService = cameraService;
        }

        // --- DTOs for API Requests/Responses ---

        public class StartRequest
        {
            public List<int> CameraIds { get; set; } = new List<int>();
            public List<string>? TargetAnimals { get; set; }
            public string? HighlightSavePath { get; set; }
        }

        public class StopRequest
        {
            public List<int> CameraIds { get; set; } = new List<int>();
        }

        public class CameraInfo
        {
            public int CameraId { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        // --- API Endpoints ---

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
                _logger.LogError(ex, "An unexpected error occurred while discovering cameras.");
                return StatusCode(500, new { message = "An error occurred while discovering cameras. Check backend logs for details." });
            }
        }

        [HttpPost("start")]
        public IActionResult StartProcessing([FromBody] StartRequest request)
        {
            if (request == null || !request.CameraIds.Any())
            {
                return BadRequest(new { message = "Request body must contain at least one CameraId." });
            }
            if (string.IsNullOrWhiteSpace(request.HighlightSavePath))
            {
                return BadRequest(new { message = "HighlightSavePath cannot be empty." });
            }

            try
            {
                var successfullyStarted = new List<int>();
                var failedToStart = new Dictionary<int, string>();

                foreach (var cameraId in request.CameraIds)
                {
                    // Ensure the camera is initialized before starting.
                    if (!_cameraService.IsCameraInitialized(cameraId))
                    {
                        if (!_cameraService.InitializeCamera(cameraId))
                        {
                            _logger.LogError("API: Failed to initialize CameraId: {CameraId}", cameraId);
                            failedToStart[cameraId] = "Failed to initialize. The camera may be disconnected or in use by another application.";
                            continue; // Skip to the next camera
                        }
                    }

                    _cameraService.StartProcessing(cameraId, request.TargetAnimals ?? new List<string>(), request.HighlightSavePath);
                    successfullyStarted.Add(cameraId);
                }

                if (failedToStart.Any())
                {
                    // If some cameras failed, return a partial success response
                    return new ObjectResult(new
                    {
                        message = $"Processing started for some cameras. Failures occurred.",
                        started = successfullyStarted,
                        failures = failedToStart
                    })
                    { StatusCode = 207 }; // 207 Multi-Status
                }

                return Ok(new { message = $"Processing started successfully for cameras: {string.Join(", ", successfullyStarted)}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting camera processing.");
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }

        [HttpPost("stop")]
        public IActionResult StopProcessing([FromBody] StopRequest request)
        {
            try
            {
                if (request.CameraIds != null && request.CameraIds.Any())
                {
                    _logger.LogInformation("API: Stopping processing for cameras: {Ids}", string.Join(", ", request.CameraIds));
                    foreach (var cameraId in request.CameraIds)
                    {
                        _cameraService.StopProcessing(cameraId);
                    }
                    return Ok(new { message = $"Processing stopped for specified cameras." });
                }
                else
                {
                    _logger.LogInformation("API: Stopping all camera processing.");
                    _cameraService.StopAllProcessing();
                    return Ok(new { message = "All camera processing stopped." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping camera processing.");
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }
    }
}
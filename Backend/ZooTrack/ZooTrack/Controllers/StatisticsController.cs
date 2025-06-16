using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZooTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly ZootrackDbContext _context;

        public StatisticsController(ZootrackDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a summary of detection statistics (True Positive, False Positive, False Negative).
        /// </summary>
        /// <param name="deviceId">Optional: Filter statistics by a specific device (camera).</param>
        /// <param name="startDate">Optional: Filter statistics from this start date (inclusive).</param>
        /// <param name="endDate">Optional: Filter statistics up to this end date (inclusive).</param>
        /// <returns>An object containing the counts of true positives, false positives, and false negatives.</returns>
        [HttpGet("summary")]
        public async Task<IActionResult> GetDetectionSummary(
            [FromQuery] int? deviceId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Start with all detection validations
                var query = _context.DetectionValidations
                                .Include(dv => dv.Detection) // Include the related Detection to access DeviceId and DetectedAt
                                .AsQueryable();

                // Apply device filter if provided
                if (deviceId.HasValue)
                {
                    query = query.Where(dv => dv.Detection.DeviceId == deviceId.Value);
                }

                // Apply date range filter if provided
                if (startDate.HasValue)
                {
                    query = query.Where(dv => dv.Detection.DetectedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    query = query.Where(dv => dv.Detection.DetectedAt <= endDate.Value);
                }

                // Calculate counts
                var truePositives = await query.CountAsync(dv => dv.IsTruePositive);
                var falsePositives = await query.CountAsync(dv => dv.IsFalsePositive);
                // False negatives are typically not directly linked to a 'detection' entry,
                // as they represent a missed detection. For a simple implementation,
                // we assume IsFalseNegative is explicitly set on a DetectionValidation
                // even if no 'detection' occurred, or you might need a separate way to log these.
                // For now, we'll count them directly from DetectionValidation.
                var falseNegatives = await query.CountAsync(dv => dv.IsFalseNegative);


                return Ok(new
                {
                    TruePositives = truePositives,
                    FalsePositives = falsePositives,
                    FalseNegatives = falseNegatives
                });
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using a logging framework)
                Console.WriteLine($"Error getting detection summary: {ex.Message}");
                return StatusCode(500, "Internal server error while retrieving detection summary.");
            }
        }

        /// <summary>
        /// Retrieves raw detection data for heatmap visualization for a specific camera within a time range.
        /// </summary>
        /// <param name="cameraId">The ID of the camera for which to retrieve heatmap data.</param>
        /// <param name="startDate">Optional: Filter detections from this start date (inclusive).</param>
        /// <param name="endDate">Optional: Filter detections up to this end date (inclusive).</param>
        /// <returns>A list of objects, each containing bounding box coordinates and confidence for a detection.</returns>
        [HttpGet("heatmap/{cameraId}")]
        public async Task<IActionResult> GetHeatmapData(
            int cameraId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var query = _context.Detections
                                .Where(d => d.DeviceId == cameraId)
                                .AsQueryable();

                // Apply date range filter if provided
                if (startDate.HasValue)
                {
                    query = query.Where(d => d.DetectedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    query = query.Where(d => d.DetectedAt <= endDate.Value);
                }

                // Select only the necessary bounding box and confidence data to send to the client.
                // This keeps the payload minimal and focused on heatmap needs.
                var heatmapData = await query
                    .Select(d => new
                    {
                        d.BoundingBoxX,
                        d.BoundingBoxY,
                        d.BoundingBoxWidth,
                        d.BoundingBoxHeight,
                        d.Confidence,
                        d.DetectedAt // Include timestamp for potential client-side filtering/animation
                    })
                    .ToListAsync();

                return Ok(heatmapData);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error getting heatmap data: {ex.Message}");
                return StatusCode(500, "Internal server error while retrieving heatmap data.");
            }
        }
    }
}

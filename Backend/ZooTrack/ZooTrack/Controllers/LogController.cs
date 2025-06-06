using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrackBackend.Services;

namespace ZooTrack.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize]
    public class LogController : ControllerBase
    {
        private readonly ILogService _logService;

        private readonly ZootrackDbContext _context;

        public LogController(ILogService logService)
        {
            _logService = logService;
        }

        // GET: api/Log
        [HttpGet]
        // [Authorize(Roles = "Admin")] // Only admins can see all logs
        public async Task<ActionResult<IEnumerable<Log>>> GetLogs(
            [FromQuery] int? userId = null,
            [FromQuery] string actionType = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string level = null,
            [FromQuery] int? detectionId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            if (pageSize > 100) pageSize = 100; // Limit maximum page size

            return Ok(await _logService.GetLogsAsync(
                userId, actionType, startDate, endDate, level, detectionId, pageNumber, pageSize));
        }

        // GET: api/Log/User/5
        [HttpGet("User/{userId}")]
        public async Task<ActionResult<IEnumerable<Log>>> GetUserLogs(
            int userId,
            [FromQuery] string actionType = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string level = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            // Check if the current user is allowed to see these logs
            if (!User.IsInRole("Admin") && int.Parse(User.FindFirst("UserId")?.Value ?? "0") != userId)
            {
                return Forbid();
            }

            if (pageSize > 100) pageSize = 100; // Limit maximum page size

            return Ok(await _logService.GetLogsAsync(
                userId, actionType, startDate, endDate, level, null, pageNumber, pageSize));
        }

        // DELETE: api/Log/5
        [HttpDelete("{id}")]
        // [Authorize(Roles = "Admin")] // Only admins can delete logs
        public async Task<IActionResult> DeleteLog(int id)
        {
            var log = await _context.Logs.FindAsync(id);
            if (log == null)
            {
                return NotFound();
            }

            _context.Logs.Remove(log);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool LogExists(int id)
        {
            return _context.Logs.Any(e => e.LogId == id);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrackBackend.Services;

namespace ZooTrack.Services
{
    public class LogService : ILogService
    {
        private readonly ZootrackDbContext _context;

        public LogService(ZootrackDbContext context)
        {
            _context = context;
        }

        // ⭐ FIXED: Gracefully handle FK constraint errors
        public async Task<Log> AddLogAsync(int? userId, string actionType, string message = "", string level = "Info", int? detectionId = null)
        {
            var log = new Log
            {
                UserId = userId,
                ActionType = actionType,
                Message = message,
                Level = level,
                Timestamp = DateTime.Now,
                DetectionId = detectionId
            };

            try
            {
                // ⭐ Validate foreign keys BEFORE trying to save
                if (userId.HasValue)
                {
                    var userExists = await _context.Users.AnyAsync(u => u.UserId == userId.Value);
                    if (!userExists)
                    {
                        // User doesn't exist - set to null to avoid FK constraint error
                        log.UserId = null;
                        Console.WriteLine($"WARNING: User {userId} doesn't exist. Log will be saved without UserId.");
                    }
                }

                if (detectionId.HasValue)
                {
                    var detectionExists = await _context.Detections.AnyAsync(d => d.DetectionId == detectionId.Value);
                    if (!detectionExists)
                    {
                        // Detection doesn't exist - set to null to avoid FK constraint error
                        log.DetectionId = null;
                        Console.WriteLine($"WARNING: Detection {detectionId} doesn't exist. Log will be saved without DetectionId.");
                    }
                }

                await _context.Logs.AddAsync(log);
                await _context.SaveChangesAsync();

                return log;
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("FOREIGN KEY constraint failed") == true)
            {
                // ⭐ FK constraint error - log to console instead and return a dummy log
                Console.WriteLine($"WARNING: Failed to save log to database due to FK constraint: {actionType} - {message}");
                Console.WriteLine($"  UserId: {userId}, DetectionId: {detectionId}");
                Console.WriteLine($"  Error: {ex.InnerException.Message}");

                // Return the log object even though it wasn't saved
                return log;
            }
            catch (Exception ex)
            {
                // ⭐ Any other error - log to console and return dummy log
                Console.WriteLine($"ERROR: Failed to save log to database: {actionType} - {message}");
                Console.WriteLine($"  Error: {ex.Message}");

                // Return the log object even though it wasn't saved
                return log;
            }
        }

        public async Task<PaginatedLogResponse> GetLogsAsync(
            int? userId = null,
            string actionType = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string level = null,
            int? detectionId = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            // Validate pagination parameters
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Logs.AsQueryable();

            // Apply filters
            if (userId.HasValue)
                query = query.Where(l => l.UserId == userId.Value);

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(l => l.ActionType.Contains(actionType));

            if (startDate.HasValue)
                query = query.Where(l => l.Timestamp >= startDate.Value);

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(l => l.Timestamp <= endOfDay);
            }

            if (!string.IsNullOrEmpty(level))
                query = query.Where(l => l.Level == level);

            if (detectionId.HasValue)
                query = query.Where(l => l.DetectionId == detectionId.Value);

            // Get total count before pagination
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            // Ensure current page is within bounds
            if (pageNumber > totalPages && totalPages > 0)
                pageNumber = totalPages;

            // Apply ordering and pagination
            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedLogResponse
            {
                Logs = logs,
                CurrentPage = pageNumber,
                TotalPages = totalPages,
                TotalRecords = totalRecords,
                PageSize = pageSize
            };
        }

        public async Task<Log> GetLogByIdAsync(int logId)
        {
            return await _context.Logs.FindAsync(logId);
        }

        public async Task<bool> DeleteLogAsync(int logId)
        {
            var log = await _context.Logs.FindAsync(logId);
            if (log == null)
                return false;

            _context.Logs.Remove(log);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
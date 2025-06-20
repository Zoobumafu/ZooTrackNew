using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZooTrack.Data;
using ZooTrack.Models;
using ZooTrackBackend.Services;

namespace ZooTrack.Services // Corrected Namespace
{
    public class LogService : ILogService
    {
        private readonly ZootrackDbContext _context;

        public LogService(ZootrackDbContext context)
        {
            _context = context;
        }

        public async Task<Log> AddLogAsync(int userId, string actionType, string message = "", string level = "Info", int? detectionId = null)
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

            await _context.Logs.AddAsync(log);
            await _context.SaveChangesAsync();

            return log;
        }

        public async Task<IEnumerable<Log>> GetLogsAsync(
            int? userId = null,
            string actionType = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string level = null,
            int? detectionId = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            var query = _context.Logs.AsQueryable();

            // Apply filters
            if (userId.HasValue)
                query = query.Where(l => l.UserId == userId.Value);

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(l => l.ActionType.Contains(actionType));

            if (startDate.HasValue)
                query = query.Where(l => l.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(l => l.Timestamp <= endDate.Value);

            if (!string.IsNullOrEmpty(level))
                query = query.Where(l => l.Level == level);

            if (detectionId.HasValue)
                query = query.Where(l => l.DetectionId == detectionId.Value);

            // Apply paging
            return await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
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

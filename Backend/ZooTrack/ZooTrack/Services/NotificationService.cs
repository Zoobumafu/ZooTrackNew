using ZooTrack.Data;
using ZooTrack.Models;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using System.Threading.Tasks;
using ZooTrackBackend.Services;


/*
 * In The FUTURE:
 * INotificationService interface and register it cleanly into your project with Dependency Injection
*/

namespace ZooTrack.Services
{
    public class NotificationService
    {
        private readonly ZootrackDbContext _context;
        private readonly ILogService _logService;

        public NotificationService(ZootrackDbContext context, ILogService logService)
        {
            _context = context;
            _logService = logService;
        }

        public async Task NotifyUserAsync(Detection detection)
        {
            // load detection with device data
            var detectionWithDevice = await _context.Detections
                .Include(d => d.Device)
                .FirstOrDefaultAsync(d => d.DetectionId == detection.DetectionId);
            
            if (detectionWithDevice == null)
                throw new Exception($"Detection {detection.DetectionId} not found.");

            var userToNotify = await _context.Users
                .Include(u => u.UserSettings)
                .Where(u => u.UserSettings.NotificationPreference != "None")
                .ToListAsync();

            foreach (var user in userToNotify)
            {
                var alert = new Alert
                {
                    Message = $"Detection from device '{detectionWithDevice.DeviceId}' " +
                              $"occured ad {detectionWithDevice.DetectedAt:G} " +
                              $"with confidence {detectionWithDevice.Confidence:F2}%",
                    
                    CreatedAt = DateTime.Now,
                    DetectionId = detection.DetectionId,
                    UserId = user.UserId,
                };
                _context.Alerts.Add(alert);

                // add log entry
                await _logService.AddLogAsync(
                   userId: user.UserId,
                   actionType: "NotificationSent",
                   message: $"Alert created for Detection {detection.DetectionId}",
                   detectionId: detection.DetectionId
               );
            }
            await _context.SaveChangesAsync();
        }

        // async helper method for logging
        public async Task AddLogAsync(int userId, string actionType)
        {
            var log = new Log
            {
                UserId = userId,
                ActionType = actionType,
                Timestamp = DateTime.Now
            };
            await _context.Logs.AddAsync(log);
        }

    }
}

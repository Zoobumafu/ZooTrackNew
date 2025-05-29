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
            try
            {
                // Load detection with device data
                var detectionWithDevice = await _context.Detections
                    .Include(d => d.Device)
                    .FirstOrDefaultAsync(d => d.DetectionId == detection.DetectionId);

                if (detectionWithDevice == null)
                {
                    await _logService.AddLogAsync(
                        userId: 1,
                        actionType: "NotificationFailed",
                        message: $"Detection {detection.DetectionId} not found for notification",
                        level: "Error",
                        detectionId: detection.DetectionId
                    );
                    throw new Exception($"Detection {detection.DetectionId} not found.");
                }

                var usersToNotify = await _context.Users
                    .Include(u => u.UserSettings)
                    .Where(u => u.UserSettings.NotificationPreference != "None")
                    .ToListAsync();

                int alertsCreated = 0;
                foreach (var user in usersToNotify)
                {
                    var alert = new Alert
                    {
                        Message = $"Detection from device '{detectionWithDevice.DeviceId}' " +
                                  $"occurred at {detectionWithDevice.DetectedAt:G} " +
                                  $"with confidence {detectionWithDevice.Confidence:F2}%",

                        CreatedAt = DateTime.Now,
                        DetectionId = detection.DetectionId,
                        UserId = user.UserId,
                    };
                    _context.Alerts.Add(alert);
                    alertsCreated++;

                    // Log notification sent to user
                    await _logService.AddLogAsync(
                       userId: user.UserId,
                       actionType: "NotificationSent",
                       message: $"Alert created for Detection {detection.DetectionId} with {detection.Confidence:F2}% confidence",
                       level: detection.Confidence >= 80.0 ? "Warning" : "Info",
                       detectionId: detection.DetectionId
                   );
                }

                await _context.SaveChangesAsync();

                // Log summary of notifications
                await _logService.AddLogAsync(
                    userId: 1, // System user
                    actionType: "NotificationsSent",
                    message: $"Created {alertsCreated} alerts for detection {detection.DetectionId}",
                    level: "Info",
                    detectionId: detection.DetectionId
                );
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync(
                    userId: 1,
                    actionType: "NotificationFailed",
                    message: $"Failed to send notifications for detection {detection.DetectionId}: {ex.Message}",
                    level: "Error",
                    detectionId: detection.DetectionId
                );
                throw;
            }
        }
    }
}

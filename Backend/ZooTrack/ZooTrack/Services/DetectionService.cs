using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;

namespace ZooTrack.Services
{
    public class DetectionService : IDetectionService
    {
        private readonly ZootrackDbContext _context;
        private readonly NotificationService _notificationService;

        public DetectionService(ZootrackDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }
        
        public async Task<Detection> CreateDetectionAsync(Detection detection)
        {
            _context.Detections.Add(detection);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyUserAsync(detection);

            return detection;
        }

        public async Task<IEnumerable<Detection>> GetDetectionsForDeviceAsync(int deviceId)
        {
            return await _context.Detections
                .Where(d => d.DeviceId == deviceId)
                .ToListAsync();
        }

    }
}

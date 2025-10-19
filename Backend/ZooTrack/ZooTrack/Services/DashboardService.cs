using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrackBackend.Services.ZooTrack.Services;

namespace ZooTrackBackend.Services
{
    // Provides backend logic for our UI dashboard
    public class DashboardService : IDashboardService
    {
        private readonly ZootrackDbContext _context;
        private readonly DateTime _systemStartTime;

        public DashboardService(ZootrackDbContext context)
        {
            _context = context;
            _systemStartTime = DateTime.Now.AddDays(-30); // Simulate system start time
        }

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var stats = await GetDashboardStatsAsync();
            var devices = await GetDeviceStatusAsync();
            var alerts = await GetRecentAlertsAsync(5);

            return new DashboardData
            {
                Stats = stats,
                Devices = devices,
                RecentAlerts = alerts
            };
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var now = DateTime.Now;
            var today = now.Date;
            var yesterday = today.AddDays(-1);

            // Get active devices count - devices active in last hour
            var activeDevicesCount = await _context.Devices
                .Where(d => d.LastActive >= now.AddHours(-1))
                .CountAsync();

            // Get today's detections count
            var todayDetections = await _context.Detections
                .Where(d => d.DetectedAt >= today && d.DetectedAt < today.AddDays(1))
                .CountAsync();

            // Get recent alerts count
            var criticalAlerts = await _context.Alerts
                .Where(a => a.CreatedAt >= yesterday)
                .CountAsync();

            // Calculate system uptime (simplified)
            var uptimeHours = (now - _systemStartTime).TotalHours;
            var uptimePercentage = Math.Min(99.9, (uptimeHours / (uptimeHours + 1)) * 100);

            return new DashboardStatsDto
            {
                ActiveDevicesCount = activeDevicesCount,
                TodayDetections = todayDetections,
                CriticalAlerts = criticalAlerts,
                SystemUptime = $"{uptimePercentage:F1}%"
            };
        }
        public async Task<List<DeviceStatusDto>> GetDeviceStatusAsync()
        {
            var now = DateTime.Now;

            var devices = await _context.Devices
                .Select(d => new DeviceStatusDto
                {
                    Id = d.DeviceId,
                    Name = $"Camera {d.DeviceId:D2}", // Generate name since your model doesn't have it
                    Location = d.Location ?? "Unknown",
                    LastSeen = d.LastActive,
                    BatteryLevel = null, // Your model doesn't have this field
                    IsOnline = d.LastActive >= now.AddMinutes(-10),
                    Status = d.Status ?? (d.LastActive >= now.AddMinutes(-10) ? "Online" :
                            d.LastActive >= now.AddHours(-1) ? "Warning" : "Offline")
                })
                .OrderBy(d => d.Id)
                .ToListAsync();

            return devices;
        }

        public async Task<List<AlertDto>> GetRecentAlertsAsync(int count = 5)
        {
            var alerts = await _context.Alerts
                .Include(a => a.Detection)
                    .ThenInclude(d => d.Device)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new AlertDto
                {
                    Id = a.AlertId,
                    Title = "Detection Alert", // Your model doesn't have Title
                    Message = a.Message,
                    Severity = "Info", // Your model doesn't have Severity, defaulting to Info
                    Timestamp = a.CreatedAt,
                    IsRead = false, // Your model doesn't have this field
                    DeviceName = a.Detection != null && a.Detection.Device != null
                        ? $"Camera {a.Detection.Device.DeviceId:D2}"
                        : null
                })
                .ToListAsync();

            return alerts;
        }
    }
}

using ZooTrack.Client.Models;

namespace ZooTrack.Client.Services
{
    public interface IDashboardClientService
    {
        Task<DashboardData?> GetDashboardDataAsync();
        Task<DashboardStatsDto?> GetDashboardStatsAsync();
        Task<List<DeviceStatusDto>?> GetDeviceStatusAsync();
        Task<List<AlertDto>?> GetRecentAlertsAsync(int count = 5);
    }
}

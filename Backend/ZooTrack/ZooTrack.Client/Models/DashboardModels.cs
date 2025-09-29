namespace ZooTrack.Client.Models
{
    public class DashboardData
    {
        public DashboardStatsDto Stats { get; set; } = new();
        public List<DeviceStatusDto> Devices { get; set; } = new();
        public List<AlertDto> RecentAlerts { get; set; } = new();
    }

    public class DashboardStatsDto
    {
        public int ActiveDevicesCount { get; set; }
        public int TodayDetections { get; set; }
        public int CriticalAlerts { get; set; } // This will be total recent alerts
        public string SystemUptime { get; set; } = "99.8%";
    }

    public class DeviceStatusDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime LastSeen { get; set; }
        public double? BatteryLevel { get; set; }
        public bool IsOnline { get; set; }
    }

    public class AlertDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string? DeviceName { get; set; }
    }
}

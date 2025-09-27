using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooTrackBackend.Services;
using ZooTrackBackend.Services.ZooTrack.Services;

namespace ZooTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardData>> GetDashboardData()
        {
            try
            {
                _logger.LogInformation("BACKEND: Dashboard API called at {Time}", DateTime.Now);

                var data = await _dashboardService.GetDashboardDataAsync();

                _logger.LogInformation("BACKEND: Stats - Active: {Active}, Detections: {Detections}, Alerts: {Alerts}",
                    data.Stats?.ActiveDevicesCount,
                    data.Stats?.TodayDetections,
                    data.Stats?.CriticalAlerts);

                _logger.LogInformation("BACKEND: Devices Count: {DeviceCount}", data.Devices?.Count);
                _logger.LogInformation("BACKEND: Recent Alerts Count: {AlertCount}", data.RecentAlerts?.Count);

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BACKEND ERROR: Failed to get dashboard data");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDto>> GetStats()
        {
            try
            {
                var stats = await _dashboardService.GetDashboardStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get dashboard stats");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("devices")]
        public async Task<ActionResult<List<DeviceStatusDto>>> GetDevices()
        {
            try
            {
                var devices = await _dashboardService.GetDeviceStatusAsync();
                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get device status");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("alerts")]
        public async Task<ActionResult<List<AlertDto>>> GetAlerts([FromQuery] int count = 5)
        {
            try
            {
                var alerts = await _dashboardService.GetRecentAlertsAsync(count);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent alerts");
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}
using System.Net.Http.Json;
using System.Text.Json;
using ZooTrack.Client.Models;

namespace ZooTrack.Client.Services
{
    public class DashboardClientService : IDashboardClientService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public DashboardClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<DashboardData?> GetDashboardDataAsync()
        {
            try
            {
                Console.WriteLine("CLIENT: Making API call to /api/dashboard");

                var response = await _httpClient.GetAsync("api/dashboard");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"CLIENT: Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API returned {response.StatusCode}: {content}");
                }

                var data = JsonSerializer.Deserialize<DashboardData>(content, _jsonOptions);
                Console.WriteLine($"CLIENT: Successfully parsed data - Devices: {data?.Devices?.Count}, Alerts: {data?.RecentAlerts?.Count}");

                return data;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"CLIENT HTTP ERROR: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CLIENT UNEXPECTED ERROR: {ex.Message}");
                throw new HttpRequestException($"Failed to get dashboard data: {ex.Message}", ex);
            }
        }

        public async Task<DashboardStatsDto?> GetDashboardStatsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<DashboardStatsDto>(
                    "api/dashboard/stats", _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching dashboard stats: {ex.Message}");
                throw;
            }
        }

        public async Task<List<DeviceStatusDto>?> GetDeviceStatusAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<DeviceStatusDto>>(
                    "api/dashboard/devices", _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching device status: {ex.Message}");
                throw;
            }
        }

        public async Task<List<AlertDto>?> GetRecentAlertsAsync(int count = 5)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<AlertDto>>(
                    $"api/dashboard/alerts?count={count}", _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching recent alerts: {ex.Message}");
                throw;
            }
        }
    }
}

using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Authorization;

namespace ZooTrack.Client.Services
{
    public interface IAuthService
    {
        Task<bool> Login(LoginModel loginModel);
        Task<bool> Register(RegisterModel registerModel);
        Task Logout();
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationStateProvider _authenticationStateProvider;

        public AuthService(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider)
        {
            _httpClient = httpClient;
            _authenticationStateProvider = authenticationStateProvider;
        }

        public async Task<bool> Login(LoginModel loginModel)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginModel);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResult>();
            if (result?.Token != null)
            {
                await ((CustomAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsAuthenticated(result.Token);
                return true;
            }
            return false;
        }

        public async Task<bool> Register(RegisterModel registerModel)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerModel);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task Logout()
        {
            await ((CustomAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsLoggedOut();
        }
    }

    public class LoginModel
    {
        [Required(ErrorMessage = "Username is required")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterModel
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain English letters, numbers, and underscores")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginResult
    {
        public string Token { get; set; } = string.Empty;
    }
}
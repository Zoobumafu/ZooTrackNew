using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using ZooTrack.Services;

namespace ZooTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class RegisterRequest
        {
            public string Username { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string ConfirmPassword { get; set; }
        }

        public class ChangePasswordRequest
        {
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            var token = await _authService.Login(request.Email, request.Password);

            if (token == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            return Ok(new { Token = token });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest("Registration data is required.");
            }

            // Validation
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Email) ||
                string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.ConfirmPassword))
            {
                return BadRequest("All fields are required.");
            }

            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest("Passwords do not match.");
            }

            if (request.Username.Length < 3 || request.Username.Length > 50)
            {
                return BadRequest("Username must be between 3 and 50 characters.");
            }

            if (request.Password.Length < 6)
            {
                return BadRequest("Password must be at least 6 characters long.");
            }

            // Check if username contains only English letters, numbers, and underscores
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]+$"))
            {
                return BadRequest("Username can only contain English letters, numbers, and underscores.");
            }

            // Check if email is valid format
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return BadRequest("Invalid email format.");
            }

            try
            {
                var success = await _authService.Register(request.Username, request.Email, request.Password);

                if (success)
                {
                    return Ok(new { Message = "Registration successful." });
                }
                else
                {
                    return BadRequest("Registration failed. Username or email might already exist.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("change-password")]
        [Authorize] // Only logged-in users can change their password
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.OldPassword) || string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest("Old and new passwords are required.");
            }

            // Get the user ID from the token claims
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var success = await _authService.ChangePassword(userId, request.OldPassword, request.NewPassword);

            if (!success)
            {
                return BadRequest("Could not change password. Please verify your old password is correct.");
            }

            return Ok(new { Message = "Password changed successfully." });
        }
    }
}
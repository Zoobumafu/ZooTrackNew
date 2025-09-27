using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZooTrack.Data;
using ZooTrack.Models;

namespace ZooTrack.Services
{
    /// <summary>
    /// Service for users authentication, managing registration, signing in and passwords.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly ZootrackDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ZootrackDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string> Login(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (user == null || !VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            {
                return null; // Authentication failed
            }

            return CreateToken(user);
        }

        public async Task<bool> Register(string username, string email, string password)
        {
            try
            {
                // Check if username already exists
                var existingUserByUsername = await _context.Users
                    .FirstOrDefaultAsync(u => u.Name.ToLower() == username.ToLower());

                if (existingUserByUsername != null)
                {
                    return false; // Username already exists
                }

                // Check if email already exists
                var existingUserByEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (existingUserByEmail != null)
                {
                    return false; // Email already exists
                }

                // Create password hash and salt
                CreatePasswordHash(password, out var passwordHash, out var passwordSalt);

                // Create new user
                var user = new User
                {
                    Name = username,
                    Email = email.ToLower(),
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    Role = "User" // Default role - adjust as needed
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ChangePassword(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            if (!VerifyPasswordHash(oldPassword, user.PasswordHash, user.PasswordSalt)) return false;

            CreatePasswordHash(newPassword, out var newPasswordHash, out var newPasswordSalt);
            user.PasswordHash = newPasswordHash;
            user.PasswordSalt = newPasswordSalt;

            await _context.SaveChangesAsync();
            return true;
        }

        private bool VerifyPasswordHash(string password, string storedHash, string storedSalt)
        {
            using (var hmac = new HMACSHA512(Convert.FromBase64String(storedSalt)))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(computedHash) == storedHash;
            }
        }

        public static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = Convert.ToBase64String(hmac.Key);
                passwordHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            }
        }

        private string CreateToken(User user)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("AppSettings:Token").Value));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature); // Changed to HmacSha256

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8), // Changed to UtcNow and 8 hours for development
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
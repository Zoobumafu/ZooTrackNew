using Microsoft.EntityFrameworkCore;
using ZooTrack.Models;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using ZooTrack.Services;

namespace ZooTrack.Data
{
    public class ZootrackDbContext : DbContext
    {
        public ZootrackDbContext(DbContextOptions<ZootrackDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<Media> Media { get; set; }
        public DbSet<Detection> Detections { get; set; }
        public DbSet<TrackingRoute> TrackingRoutes { get; set; }
        public DbSet<Animal> Animals { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<DetectionValidation> DetectionValidations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<UserSettings>()
                .HasOne(us => us.User)
                .WithOne(u => u.UserSettings)
                .HasForeignKey<UserSettings>(us => us.UserId);

            // --- SEED SINGLE ADMIN USER WITH NEW CREDENTIALS ---
            // Calls the static method on AuthService to create the password hash and salt
            AuthService.CreatePasswordHash("Admin", out var adminHash, out var adminSalt);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId = 1,
                    Name = "Admin",
                    Email = "Admin", // Simplified username
                    Role = "Admin",
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt
                }
            );

            // Other seed data...
            modelBuilder.Entity<UserSettings>().HasData(
                new UserSettings
                {
                    UserId = 1,
                    NotificationPreference = "Email",
                    DetectionThreshold = 0.8f,
                    TargetAnimalsJson = JsonSerializer.Serialize(new List<string> { "person", "dog", "cow", "wolf", "tiger", "lion" }),
                    HighlightSavePath = "Media/HighlightFrames/Admin"
                }
            );

            modelBuilder.Entity<Device>().HasData(
                new Device { DeviceId = 1, Location = "North Zone", Status = "Online", LastActive = new DateTime(2025, 6, 18, 18, 18, 18) }
            );
        }
    }
}

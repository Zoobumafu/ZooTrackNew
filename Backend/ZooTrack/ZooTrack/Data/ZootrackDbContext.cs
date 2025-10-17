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

            // === USERS SEED DATA ===
            var adminSalt = "AQIDBAUEDF+CAQIDBAUGCQ0ODw==";
            var adminHash = "ERITFBUWFxgZGhscHR4fICEiIyQlJicoKSorLC0uLzAx";
            var managerSalt = "ITIzNDU2Nzg5Ojs8PT4/QEFCQ0RFRkdISUpLTE1OT1A=";
            var managerHash = "UVJTVFVWVxhZWltcXV5fYGFiY2RlZmdoaWprbG1ub3A=";
            var userSalt = "cXJzdHV2d3h5ent8fX5/gIGCg4SFhoeIiYqLjI2Oj5A=";
            var userHash = "kZKTlJWWl5iZmpucnZ6foKGio6SlpqeoqaqrrK2ur7A=";

            modelBuilder.Entity<User>().HasData(
                new User { UserId = 1, Name = "Admin", Email = "Admin", Role = "Admin", PasswordHash = adminHash, PasswordSalt = adminSalt },
                new User { UserId = 2, Name = "Zoo Manager", Email = "manager@zootrack.com", Role = "Manager", PasswordHash = managerHash, PasswordSalt = managerSalt },
                new User { UserId = 3, Name = "Wildlife Observer", Email = "observer@zootrack.com", Role = "User", PasswordHash = userHash, PasswordSalt = userSalt }
            );

            // === USER SETTINGS SEED DATA ===
            modelBuilder.Entity<UserSettings>().HasData(
                new UserSettings { UserId = 1, NotificationPreference = "Email", DetectionThreshold = 0.8f, TargetAnimalsJson = "[\"person\",\"dog\",\"cow\",\"wolf\",\"tiger\",\"lion\",\"elephant\",\"giraffe\"]", HighlightSavePath = "Media/HighlightFrames/Admin" },
                new UserSettings { UserId = 2, NotificationPreference = "SMS", DetectionThreshold = 0.75f, TargetAnimalsJson = "[\"tiger\",\"lion\",\"elephant\",\"bear\",\"wolf\"]", HighlightSavePath = "Media/HighlightFrames/Manager" },
                new UserSettings { UserId = 3, NotificationPreference = "Email", DetectionThreshold = 0.85f, TargetAnimalsJson = "[\"bird\",\"deer\",\"fox\",\"rabbit\"]", HighlightSavePath = "Media/HighlightFrames/Observer" }
            );

            // FIX: Replaced all hardcoded dates with dynamic dates relative to the current time.
            // var now = DateTime.Now;

            // === DEVICES SEED DATA ===
            // === DEVICES SEED DATA ===
            modelBuilder.Entity<Device>().HasData(
                new Device { DeviceId = 1, Location = "North Zone", Status = "Online", LastActive = new DateTime(2025, 10, 17, 14, 55, 0) },
                new Device { DeviceId = 2, Location = "South Zone", Status = "Online", LastActive = new DateTime(2025, 10, 17, 14, 50, 0) },
                new Device { DeviceId = 3, Location = "East Zone", Status = "Offline", LastActive = new DateTime(2025, 10, 16, 15, 0, 0) },
                new Device { DeviceId = 4, Location = "West Zone", Status = "Online", LastActive = new DateTime(2025, 10, 17, 14, 58, 0) },
                new Device { DeviceId = 5, Location = "Central Zone", Status = "Maintenance", LastActive = new DateTime(2025, 10, 17, 11, 0, 0) }
            );

            // === EVENTS SEED DATA ===
            modelBuilder.Entity<Event>().HasData(
                new Event { EventId = 1, StartTime = new DateTime(2025, 10, 15, 15, 0, 0), EndTime = new DateTime(2025, 10, 15, 16, 0, 0), Status = "Completed" },
                new Event { EventId = 2, StartTime = new DateTime(2025, 10, 16, 15, 0, 0), EndTime = new DateTime(2025, 10, 16, 16, 0, 0), Status = "Completed" },
                new Event { EventId = 3, StartTime = new DateTime(2025, 10, 17, 11, 0, 0), EndTime = new DateTime(2025, 10, 17, 12, 0, 0), Status = "Completed" },
                new Event { EventId = 4, StartTime = new DateTime(2025, 10, 17, 14, 0, 0), EndTime = new DateTime(2025, 10, 17, 16, 0, 0), Status = "Active" },
                new Event { EventId = 5, StartTime = new DateTime(2025, 10, 17, 15, 0, 0), EndTime = new DateTime(2025, 10, 17, 17, 0, 0), Status = "Active" }
            );

            // === MEDIA SEED DATA ===
            modelBuilder.Entity<Media>().HasData(
                new Media { MediaId = 1, Type = "Video", FilePath = "Media/Videos/video1.mp4", Timestamp = new DateTime(2025, 10, 15, 15, 0, 0), DeviceId = 1 },
                new Media { MediaId = 2, Type = "Image", FilePath = "Media/Images/image1.jpg", Timestamp = new DateTime(2025, 10, 15, 15, 3, 0), DeviceId = 1 },
                new Media { MediaId = 3, Type = "Video", FilePath = "Media/Videos/video2.mp4", Timestamp = new DateTime(2025, 10, 16, 15, 0, 0), DeviceId = 2 },
                new Media { MediaId = 4, Type = "Image", FilePath = "Media/Images/image2.jpg", Timestamp = new DateTime(2025, 10, 16, 15, 5, 0), DeviceId = 2 },
                new Media { MediaId = 5, Type = "Video", FilePath = "Media/Videos/video3.mp4", Timestamp = new DateTime(2025, 10, 17, 11, 0, 0), DeviceId = 4 }
            );

            // === DETECTIONS SEED DATA ===
            modelBuilder.Entity<Detection>().HasData(
                new Detection { DetectionId = 1, Confidence = 92f, DetectedAt = new DateTime(2025, 10, 15, 15, 3, 0), DeviceId = 1, DetectedObject = "tiger", MediaId = 1, EventId = 1 },
                new Detection { DetectionId = 2, Confidence = 95f, DetectedAt = new DateTime(2025, 10, 16, 15, 2, 0), DeviceId = 2, DetectedObject = "elephant", MediaId = 3, EventId = 2 },
                new Detection { DetectionId = 3, Confidence = 87f, DetectedAt = new DateTime(2025, 10, 16, 15, 5, 0), DeviceId = 2, DetectedObject = "giraffe", MediaId = 4, EventId = 2 },
                new Detection { DetectionId = 4, Confidence = 91f, DetectedAt = new DateTime(2025, 10, 17, 11, 2, 0), DeviceId = 4, DetectedObject = "lion", MediaId = 5, EventId = 3 },
                new Detection { DetectionId = 5, Confidence = 89f, DetectedAt = new DateTime(2025, 10, 17, 11, 5, 0), DeviceId = 4, DetectedObject = "wolf", MediaId = 5, EventId = 3 },
                new Detection { DetectionId = 6, Confidence = 94f, DetectedAt = new DateTime(2025, 10, 17, 14, 45, 0), DeviceId = 1, DetectedObject = "bear", MediaId = 1, EventId = 5 }
            );

            // === LOGS SEED DATA ===
            modelBuilder.Entity<Log>().HasData(
                new Log { LogId = 1, UserId = 1, ActionType = "Login", Timestamp = new DateTime(2025, 10, 17, 12, 0, 0), Message = "Admin logged into system", Level = "Info" },
                new Log { LogId = 2, UserId = 1, ActionType = "Detection_Review", Timestamp = new DateTime(2025, 10, 17, 13, 0, 0), Message = "Validated tiger detection", Level = "Info", DetectionId = 1 },
                new Log { LogId = 3, UserId = 2, ActionType = "Login", Timestamp = new DateTime(2025, 10, 17, 14, 0, 0), Message = "Zoo Manager logged into system", Level = "Info" }
            );
        }
    }
}
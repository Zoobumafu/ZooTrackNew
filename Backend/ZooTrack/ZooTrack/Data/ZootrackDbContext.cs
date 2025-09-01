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
            AuthService.CreatePasswordHash("Admin", out var adminHash, out var adminSalt);
            AuthService.CreatePasswordHash("manager123", out var managerHash, out var managerSalt);
            AuthService.CreatePasswordHash("user123", out var userHash, out var userSalt);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId = 1,
                    Name = "Admin",
                    Email = "Admin",
                    Role = "Admin",
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt
                },
                new User
                {
                    UserId = 2,
                    Name = "Zoo Manager",
                    Email = "manager@zootrack.com",
                    Role = "Manager",
                    PasswordHash = managerHash,
                    PasswordSalt = managerSalt
                },
                new User
                {
                    UserId = 3,
                    Name = "Wildlife Observer",
                    Email = "observer@zootrack.com",
                    Role = "User",
                    PasswordHash = userHash,
                    PasswordSalt = userSalt
                }
            );

            // === USER SETTINGS SEED DATA ===
            modelBuilder.Entity<UserSettings>().HasData(
                new UserSettings
                {
                    UserId = 1,
                    NotificationPreference = "Email",
                    DetectionThreshold = 0.8f,
                    TargetAnimalsJson = JsonSerializer.Serialize(new List<string> { "person", "dog", "cow", "wolf", "tiger", "lion", "elephant", "giraffe" }),
                    HighlightSavePath = "Media/HighlightFrames/Admin"
                },
                new UserSettings
                {
                    UserId = 2,
                    NotificationPreference = "SMS",
                    DetectionThreshold = 0.75f,
                    TargetAnimalsJson = JsonSerializer.Serialize(new List<string> { "tiger", "lion", "elephant", "bear", "wolf" }),
                    HighlightSavePath = "Media/HighlightFrames/Manager"
                },
                new UserSettings
                {
                    UserId = 3,
                    NotificationPreference = "Email",
                    DetectionThreshold = 0.85f,
                    TargetAnimalsJson = JsonSerializer.Serialize(new List<string> { "bird", "deer", "fox", "rabbit" }),
                    HighlightSavePath = "Media/HighlightFrames/Observer"
                }
            );

            // === DEVICES SEED DATA ===
            modelBuilder.Entity<Device>().HasData(
                new Device { DeviceId = 1, Location = "North Zone", Status = "Online", LastActive = new DateTime(2025, 6, 20, 14, 30, 0) },
                new Device { DeviceId = 2, Location = "South Zone", Status = "Online", LastActive = new DateTime(2025, 6, 20, 14, 25, 0) },
                new Device { DeviceId = 3, Location = "East Zone", Status = "Offline", LastActive = new DateTime(2025, 6, 19, 22, 45, 0) },
                new Device { DeviceId = 4, Location = "West Zone", Status = "Online", LastActive = new DateTime(2025, 6, 20, 14, 35, 0) },
                new Device { DeviceId = 5, Location = "Central Zone", Status = "Maintenance", LastActive = new DateTime(2025, 6, 20, 8, 15, 0) }
            );

            // === EVENTS SEED DATA ===
            modelBuilder.Entity<Event>().HasData(
                new Event { EventId = 1, StartTime = new DateTime(2025, 6, 19, 10, 15, 0), EndTime = new DateTime(2025, 6, 19, 10, 25, 0), Status = "Completed" },
                new Event { EventId = 2, StartTime = new DateTime(2025, 6, 19, 14, 30, 0), EndTime = new DateTime(2025, 6, 19, 14, 45, 0), Status = "Completed" },
                new Event { EventId = 3, StartTime = new DateTime(2025, 6, 20, 8, 20, 0), EndTime = new DateTime(2025, 6, 20, 8, 35, 0), Status = "Completed" },
                new Event { EventId = 4, StartTime = new DateTime(2025, 6, 20, 12, 10, 0), EndTime = new DateTime(2025, 6, 20, 12, 20, 0), Status = "Active" },
                new Event { EventId = 5, StartTime = new DateTime(2025, 6, 20, 15, 0, 0), EndTime = new DateTime(2025, 6, 20, 15, 15, 0), Status = "Active" }
            );

            // === MEDIA SEED DATA ===
            modelBuilder.Entity<Media>().HasData(
                new Media { MediaId = 1, Type = "Video", FilePath = "Media/Videos/north_zone_20250619_101500.jpg", Timestamp = new DateTime(2025, 6, 19, 10, 15, 0), DeviceId = 1 },
                new Media { MediaId = 2, Type = "Image", FilePath = "Media/Images/north_zone_20250619_101800.jpg", Timestamp = new DateTime(2025, 6, 19, 10, 18, 0), DeviceId = 1 },
                new Media { MediaId = 3, Type = "Video", FilePath = "Media/Videos/south_zone_20250619_143000.jpg", Timestamp = new DateTime(2025, 6, 19, 14, 30, 0), DeviceId = 2 },
                new Media { MediaId = 4, Type = "Image", FilePath = "Media/Images/south_zone_20250619_143500.jpg", Timestamp = new DateTime(2025, 6, 19, 14, 35, 0), DeviceId = 2 },
                new Media { MediaId = 5, Type = "Video", FilePath = "Media/Videos/west_zone_20250620_082000.jpg", Timestamp = new DateTime(2025, 6, 20, 8, 20, 0), DeviceId = 4 },
                new Media { MediaId = 6, Type = "Image", FilePath = "Media/Images/central_zone_20250620_121000.jpg", Timestamp = new DateTime(2025, 6, 20, 12, 10, 0), DeviceId = 5 },
                new Media { MediaId = 7, Type = "Video", FilePath = "Media/Videos/north_zone_20250620_150000.jpg", Timestamp = new DateTime(2025, 6, 20, 15, 0, 0), DeviceId = 1 }
            );

            // === DETECTIONS SEED DATA ===
            modelBuilder.Entity<Detection>().HasData(
                new Detection { DetectionId = 1, Confidence = 0.92f, DetectedAt = new DateTime(2025, 6, 19, 10, 18, 30), DeviceId = 1, TrackingId = 1001, BoundingBoxX = 120.5f, BoundingBoxY = 80.2f, BoundingBoxWidth = 150.0f, BoundingBoxHeight = 200.0f, FrameNumber = 450, DetectedObject = "tiger", MediaId = 1, EventId = 1 },
                new Detection { DetectionId = 2, Confidence = 0.88f, DetectedAt = new DateTime(2025, 6, 19, 10, 19, 15), DeviceId = 1, TrackingId = 1001, BoundingBoxX = 125.0f, BoundingBoxY = 85.0f, BoundingBoxWidth = 148.0f, BoundingBoxHeight = 195.0f, FrameNumber = 495, DetectedObject = "tiger", MediaId = 1, EventId = 1 },
                new Detection { DetectionId = 3, Confidence = 0.95f, DetectedAt = new DateTime(2025, 6, 19, 14, 32, 0), DeviceId = 2, TrackingId = 2001, BoundingBoxX = 200.0f, BoundingBoxY = 150.0f, BoundingBoxWidth = 180.0f, BoundingBoxHeight = 250.0f, FrameNumber = 720, DetectedObject = "elephant", MediaId = 3, EventId = 2 },
                new Detection { DetectionId = 4, Confidence = 0.87f, DetectedAt = new DateTime(2025, 6, 19, 14, 35, 30), DeviceId = 2, TrackingId = 2002, BoundingBoxX = 50.0f, BoundingBoxY = 100.0f, BoundingBoxWidth = 120.0f, BoundingBoxHeight = 160.0f, FrameNumber = 0, DetectedObject = "giraffe", MediaId = 4, EventId = 2 },
                new Detection { DetectionId = 5, Confidence = 0.91f, DetectedAt = new DateTime(2025, 6, 20, 8, 22, 45), DeviceId = 4, TrackingId = 4001, BoundingBoxX = 300.0f, BoundingBoxY = 200.0f, BoundingBoxWidth = 100.0f, BoundingBoxHeight = 130.0f, FrameNumber = 165, DetectedObject = "lion", MediaId = 5, EventId = 3 },
                new Detection { DetectionId = 6, Confidence = 0.89f, DetectedAt = new DateTime(2025, 6, 20, 8, 25, 0), DeviceId = 4, TrackingId = 4002, BoundingBoxX = 180.0f, BoundingBoxY = 120.0f, BoundingBoxWidth = 90.0f, BoundingBoxHeight = 110.0f, FrameNumber = 300, DetectedObject = "wolf", MediaId = 5, EventId = 3 },
                new Detection { DetectionId = 7, Confidence = 0.83f, DetectedAt = new DateTime(2025, 6, 20, 12, 12, 30), DeviceId = 5, TrackingId = 5001, BoundingBoxX = 150.0f, BoundingBoxY = 90.0f, BoundingBoxWidth = 80.0f, BoundingBoxHeight = 100.0f, FrameNumber = 0, DetectedObject = "deer", MediaId = 6, EventId = 4 },
                new Detection { DetectionId = 8, Confidence = 0.94f, DetectedAt = new DateTime(2025, 6, 20, 15, 2, 15), DeviceId = 1, TrackingId = 1002, BoundingBoxX = 220.0f, BoundingBoxY = 180.0f, BoundingBoxWidth = 140.0f, BoundingBoxHeight = 180.0f, FrameNumber = 135, DetectedObject = "bear", MediaId = 7, EventId = 5 },
                // More detections for North Zone (Device 1)
                new Detection { DetectionId = 9, Confidence = 0.76f, DetectedAt = new DateTime(2025, 6, 18, 14, 20, 0), DeviceId = 1, TrackingId = 1003, BoundingBoxX = 0.2f, BoundingBoxY = 0.3f, BoundingBoxWidth = 0.15f, BoundingBoxHeight = 0.25f, FrameNumber = 200, DetectedObject = "tiger", MediaId = 1, EventId = 1 },
                new Detection { DetectionId = 10, Confidence = 0.45f, DetectedAt = new DateTime(2025, 6, 18, 16, 45, 0), DeviceId = 1, TrackingId = 1004, BoundingBoxX = 0.6f, BoundingBoxY = 0.1f, BoundingBoxWidth = 0.12f, BoundingBoxHeight = 0.18f, FrameNumber = 300, DetectedObject = "deer", MediaId = 1, EventId = 1 },
                new Detection { DetectionId = 11, Confidence = 0.92f, DetectedAt = new DateTime(2025, 6, 17, 10, 30, 0), DeviceId = 1, TrackingId = 1005, BoundingBoxX = 0.4f, BoundingBoxY = 0.5f, BoundingBoxWidth = 0.2f, BoundingBoxHeight = 0.3f, FrameNumber = 150, DetectedObject = "lion", MediaId = 1, EventId = 1 },

                // More detections for South Zone (Device 2)
                new Detection { DetectionId = 12, Confidence = 0.38f, DetectedAt = new DateTime(2025, 6, 18, 11, 15, 0), DeviceId = 2, TrackingId = 2003, BoundingBoxX = 0.1f, BoundingBoxY = 0.2f, BoundingBoxWidth = 0.08f, BoundingBoxHeight = 0.12f, FrameNumber = 100, DetectedObject = "bird", MediaId = 3, EventId = 2 },
                new Detection { DetectionId = 13, Confidence = 0.89f, DetectedAt = new DateTime(2025, 6, 17, 15, 20, 0), DeviceId = 2, TrackingId = 2004, BoundingBoxX = 0.3f, BoundingBoxY = 0.4f, BoundingBoxWidth = 0.25f, BoundingBoxHeight = 0.35f, FrameNumber = 250, DetectedObject = "elephant", MediaId = 3, EventId = 2 },
                new Detection { DetectionId = 14, Confidence = 0.55f, DetectedAt = new DateTime(2025, 6, 16, 13, 45, 0), DeviceId = 2, TrackingId = 2005, BoundingBoxX = 0.7f, BoundingBoxY = 0.6f, BoundingBoxWidth = 0.1f, BoundingBoxHeight = 0.15f, FrameNumber = 180, DetectedObject = "wolf", MediaId = 3, EventId = 2 },

                // More detections for East Zone (Device 3) - Note: Device is offline but has historical data
                new Detection { DetectionId = 15, Confidence = 0.91f, DetectedAt = new DateTime(2025, 6, 15, 9, 30, 0), DeviceId = 3, TrackingId = 3001, BoundingBoxX = 0.5f, BoundingBoxY = 0.3f, BoundingBoxWidth = 0.18f, BoundingBoxHeight = 0.28f, FrameNumber = 120, DetectedObject = "bear", MediaId = 1, EventId = 1 },
                new Detection { DetectionId = 16, Confidence = 0.42f, DetectedAt = new DateTime(2025, 6, 15, 14, 20, 0), DeviceId = 3, TrackingId = 3002, BoundingBoxX = 0.2f, BoundingBoxY = 0.7f, BoundingBoxWidth = 0.06f, BoundingBoxHeight = 0.1f, FrameNumber = 90, DetectedObject = "rabbit", MediaId = 1, EventId = 1 },

                // More detections for West Zone (Device 4)
                new Detection { DetectionId = 17, Confidence = 0.67f, DetectedAt = new DateTime(2025, 6, 19, 8, 45, 0), DeviceId = 4, TrackingId = 4003, BoundingBoxX = 0.8f, BoundingBoxY = 0.2f, BoundingBoxWidth = 0.15f, BoundingBoxHeight = 0.2f, FrameNumber = 75, DetectedObject = "fox", MediaId = 5, EventId = 3 },
                new Detection { DetectionId = 18, Confidence = 0.29f, DetectedAt = new DateTime(2025, 6, 18, 17, 30, 0), DeviceId = 4, TrackingId = 4004, BoundingBoxX = 0.1f, BoundingBoxY = 0.8f, BoundingBoxWidth = 0.05f, BoundingBoxHeight = 0.08f, FrameNumber = 45, DetectedObject = "bird", MediaId = 5, EventId = 3 },

                // More detections for Central Zone (Device 5)
                new Detection { DetectionId = 19, Confidence = 0.84f, DetectedAt = new DateTime(2025, 6, 19, 16, 10, 0), DeviceId = 5, TrackingId = 5002, BoundingBoxX = 0.45f, BoundingBoxY = 0.35f, BoundingBoxWidth = 0.2f, BoundingBoxHeight = 0.3f, FrameNumber = 220, DetectedObject = "deer", MediaId = 6, EventId = 4 },
                new Detection { DetectionId = 20, Confidence = 0.95f, DetectedAt = new DateTime(2025, 6, 20, 11, 5, 0), DeviceId = 5, TrackingId = 5003, BoundingBoxX = 0.6f, BoundingBoxY = 0.4f, BoundingBoxWidth = 0.22f, BoundingBoxHeight = 0.32f, FrameNumber = 280, DetectedObject = "tiger", MediaId = 6, EventId = 4 }
            );

            // === ANIMALS SEED DATA ===
            modelBuilder.Entity<Animal>().HasData(
                new Animal { AnimalId = 1, Species = "Siberian Tiger", ConfidenceLevel = 0.92f, DetectionId = 1 },
                new Animal { AnimalId = 2, Species = "Siberian Tiger", ConfidenceLevel = 0.88f, DetectionId = 2 },
                new Animal { AnimalId = 3, Species = "African Elephant", ConfidenceLevel = 0.95f, DetectionId = 3 },
                new Animal { AnimalId = 4, Species = "Reticulated Giraffe", ConfidenceLevel = 0.87f, DetectionId = 4 },
                new Animal { AnimalId = 5, Species = "African Lion", ConfidenceLevel = 0.91f, DetectionId = 5 },
                new Animal { AnimalId = 6, Species = "Gray Wolf", ConfidenceLevel = 0.89f, DetectionId = 6 },
                new Animal { AnimalId = 7, Species = "White-tailed Deer", ConfidenceLevel = 0.83f, DetectionId = 7 },
                new Animal { AnimalId = 8, Species = "Brown Bear", ConfidenceLevel = 0.94f, DetectionId = 8 }
            );

            // === TRACKING ROUTES SEED DATA ===
            modelBuilder.Entity<TrackingRoute>().HasData(
                new TrackingRoute { Id = 1, TrackingId = 1001, DeviceId = 1, DetectedObject = "tiger", StartTime = new DateTime(2025, 6, 19, 10, 18, 30), EndTime = new DateTime(2025, 6, 19, 10, 19, 15), PathJson = "[[120.5, 80.2], [122.0, 82.0], [125.0, 85.0]]" },
                new TrackingRoute { Id = 2, TrackingId = 2001, DeviceId = 2, DetectedObject = "elephant", StartTime = new DateTime(2025, 6, 19, 14, 32, 0), EndTime = new DateTime(2025, 6, 19, 14, 35, 0), PathJson = "[[200.0, 150.0], [205.0, 155.0], [210.0, 160.0]]" },
                new TrackingRoute { Id = 3, TrackingId = 4001, DeviceId = 4, DetectedObject = "lion", StartTime = new DateTime(2025, 6, 20, 8, 22, 45), EndTime = new DateTime(2025, 6, 20, 8, 25, 0), PathJson = "[[300.0, 200.0], [295.0, 205.0], [290.0, 210.0]]" },
                new TrackingRoute { Id = 4, TrackingId = 4002, DeviceId = 4, DetectedObject = "wolf", StartTime = new DateTime(2025, 6, 20, 8, 25, 0), EndTime = new DateTime(2025, 6, 20, 8, 27, 30), PathJson = "[[180.0, 120.0], [175.0, 125.0], [170.0, 130.0]]" },
                new TrackingRoute { Id = 5, TrackingId = 1002, DeviceId = 1, DetectedObject = "bear", StartTime = new DateTime(2025, 6, 20, 15, 2, 15), EndTime = new DateTime(2025, 6, 20, 15, 5, 0), PathJson = "[[220.0, 180.0], [225.0, 185.0], [230.0, 190.0]]" },
                new TrackingRoute { Id = 6, TrackingId = 1003, DeviceId = 1, DetectedObject = "tiger", StartTime = new DateTime(2025, 6, 18, 14, 20, 0), EndTime = new DateTime(2025, 6, 18, 14, 25, 0), PathJson = "[[0.2, 0.3], [0.22, 0.32], [0.25, 0.35], [0.28, 0.38]]" },
                new TrackingRoute { Id = 7, TrackingId = 1005, DeviceId = 1, DetectedObject = "lion", StartTime = new DateTime(2025, 6, 17, 10, 30, 0), EndTime = new DateTime(2025, 6, 17, 10, 35, 0), PathJson = "[[0.4, 0.5], [0.42, 0.48], [0.45, 0.46], [0.48, 0.44]]" },
                new TrackingRoute { Id = 8, TrackingId = 2004, DeviceId = 2, DetectedObject = "elephant", StartTime = new DateTime(2025, 6, 17, 15, 20, 0), EndTime = new DateTime(2025, 6, 17, 15, 30, 0), PathJson = "[[0.3, 0.4], [0.32, 0.42], [0.34, 0.44], [0.36, 0.46], [0.38, 0.48]]" },
                new TrackingRoute { Id = 9, TrackingId = 3001, DeviceId = 3, DetectedObject = "bear", StartTime = new DateTime(2025, 6, 15, 9, 30, 0), EndTime = new DateTime(2025, 6, 15, 9, 40, 0), PathJson = "[[0.5, 0.3], [0.52, 0.32], [0.54, 0.34], [0.56, 0.36]]" },
                new TrackingRoute { Id = 10, TrackingId = 5002, DeviceId = 5, DetectedObject = "deer", StartTime = new DateTime(2025, 6, 19, 16, 10, 0), EndTime = new DateTime(2025, 6, 19, 16, 15, 0), PathJson = "[[0.45, 0.35], [0.47, 0.37], [0.49, 0.39], [0.51, 0.41]]" },
                new TrackingRoute { Id = 11, TrackingId = 5003, DeviceId = 5, DetectedObject = "tiger", StartTime = new DateTime(2025, 6, 20, 11, 5, 0), EndTime = new DateTime(2025, 6, 20, 11, 12, 0), PathJson = "[[0.6, 0.4], [0.58, 0.42], [0.56, 0.44], [0.54, 0.46], [0.52, 0.48]]" }
            );

            // === ALERTS SEED DATA ===
            modelBuilder.Entity<Alert>().HasData(
                new Alert { AlertId = 1, Message = "High confidence tiger detection in North Zone", CreatedAt = new DateTime(2025, 6, 19, 10, 18, 35), DetectionId = 1, UserId = 1 },
                new Alert { AlertId = 2, Message = "Large animal detected in South Zone - requires attention", CreatedAt = new DateTime(2025, 6, 19, 14, 32, 5), DetectionId = 3, UserId = 2 },
                new Alert { AlertId = 3, Message = "Predator activity detected in West Zone", CreatedAt = new DateTime(2025, 6, 20, 8, 22, 50), DetectionId = 5, UserId = 1 },
                new Alert { AlertId = 4, Message = "Wolf pack movement detected in West Zone", CreatedAt = new DateTime(2025, 6, 20, 8, 25, 5), DetectionId = 6, UserId = 2 },
                new Alert { AlertId = 5, Message = "Bear sighting in North Zone - immediate attention required", CreatedAt = new DateTime(2025, 6, 20, 15, 2, 20), DetectionId = 8, UserId = 1 }
            );

            // === DETECTION VALIDATIONS SEED DATA ===
            modelBuilder.Entity<DetectionValidation>().HasData(
                new DetectionValidation { Id = 1, DetectionId = 1, IsValidated = true, IsTruePositive = true, ValidationNotes = "Confirmed tiger identification", ValidatedAt = new DateTime(2025, 6, 19, 11, 0, 0), ValidatedBy = "Admin", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 2, DetectionId = 2, IsValidated = true, IsTruePositive = true, ValidationNotes = "Same tiger - tracking confirmed", ValidatedAt = new DateTime(2025, 6, 19, 11, 5, 0), ValidatedBy = "Admin", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 3, DetectionId = 3, IsValidated = true, IsTruePositive = true, ValidationNotes = "Adult elephant confirmed", ValidatedAt = new DateTime(2025, 6, 19, 15, 0, 0), ValidatedBy = "Zoo Manager", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 4, DetectionId = 4, IsValidated = false, IsTruePositive = false, ValidationNotes = "", ValidatedAt = new DateTime(2025, 6, 20, 0, 0, 0), ValidatedBy = "", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 5, DetectionId = 7, IsValidated = true, IsTruePositive = false, ValidationNotes = "False positive - was a large log", ValidatedAt = new DateTime(2025, 6, 20, 13, 0, 0), ValidatedBy = "Wildlife Observer", IsFalsePositive = true, IsFalseNegative = false },
                // True Positives
                new DetectionValidation { Id = 6, DetectionId = 9, IsValidated = true, IsTruePositive = true, ValidationNotes = "Tiger confirmed in North Zone", ValidatedAt = new DateTime(2025, 6, 18, 15, 0, 0), ValidatedBy = "Admin", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 7, DetectionId = 11, IsValidated = true, IsTruePositive = true, ValidationNotes = "Lion sighting verified", ValidatedAt = new DateTime(2025, 6, 17, 11, 0, 0), ValidatedBy = "Zoo Manager", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 8, DetectionId = 13, IsValidated = true, IsTruePositive = true, ValidationNotes = "Elephant behavior normal", ValidatedAt = new DateTime(2025, 6, 17, 16, 0, 0), ValidatedBy = "Wildlife Observer", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 9, DetectionId = 15, IsValidated = true, IsTruePositive = true, ValidationNotes = "Bear spotted in East Zone", ValidatedAt = new DateTime(2025, 6, 15, 10, 0, 0), ValidatedBy = "Admin", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 10, DetectionId = 19, IsValidated = true, IsTruePositive = true, ValidationNotes = "Deer grazing - normal behavior", ValidatedAt = new DateTime(2025, 6, 19, 17, 0, 0), ValidatedBy = "Wildlife Observer", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 11, DetectionId = 20, IsValidated = true, IsTruePositive = true, ValidationNotes = "High confidence tiger detection", ValidatedAt = new DateTime(2025, 6, 20, 12, 0, 0), ValidatedBy = "Zoo Manager", IsFalsePositive = false, IsFalseNegative = false },

                // False Positives (low confidence detections that were incorrectly identified)
                new DetectionValidation { Id = 12, DetectionId = 10, IsValidated = true, IsTruePositive = false, ValidationNotes = "Shadow mistaken for deer", ValidatedAt = new DateTime(2025, 6, 18, 17, 0, 0), ValidatedBy = "Admin", IsFalsePositive = true, IsFalseNegative = false },
                new DetectionValidation { Id = 13, DetectionId = 12, IsValidated = true, IsTruePositive = false, ValidationNotes = "Leaf movement, not a bird", ValidatedAt = new DateTime(2025, 6, 18, 12, 0, 0), ValidatedBy = "Wildlife Observer", IsFalsePositive = true, IsFalseNegative = false },
                new DetectionValidation { Id = 14, DetectionId = 14, IsValidated = true, IsTruePositive = false, ValidationNotes = "Rock formation misidentified as wolf", ValidatedAt = new DateTime(2025, 6, 16, 14, 30, 0), ValidatedBy = "Zoo Manager", IsFalsePositive = true, IsFalseNegative = false },
                new DetectionValidation { Id = 15, DetectionId = 16, IsValidated = true, IsTruePositive = false, ValidationNotes = "Small debris, not rabbit", ValidatedAt = new DateTime(2025, 6, 15, 15, 0, 0), ValidatedBy = "Admin", IsFalsePositive = true, IsFalseNegative = false },
                new DetectionValidation { Id = 16, DetectionId = 17, IsValidated = true, IsTruePositive = false, ValidationNotes = "Bush movement mistaken for fox", ValidatedAt = new DateTime(2025, 6, 19, 9, 0, 0), ValidatedBy = "Wildlife Observer", IsFalsePositive = true, IsFalseNegative = false },
                new DetectionValidation { Id = 17, DetectionId = 18, IsValidated = true, IsTruePositive = false, ValidationNotes = "Flying plastic bag, not bird", ValidatedAt = new DateTime(2025, 6, 18, 18, 0, 0), ValidatedBy = "Zoo Manager", IsFalsePositive = true, IsFalseNegative = false },

                // Unvalidated detections (these won't count in statistics until validated)
                new DetectionValidation { Id = 18, DetectionId = 6, IsValidated = false, IsTruePositive = false, ValidationNotes = "", ValidatedAt = new DateTime(2025, 6, 20, 0, 0, 0), ValidatedBy = "", IsFalsePositive = false, IsFalseNegative = false },
                new DetectionValidation { Id = 19, DetectionId = 8, IsValidated = false, IsTruePositive = false, ValidationNotes = "", ValidatedAt = new DateTime(2025, 6, 20, 0, 0, 0), ValidatedBy = "", IsFalsePositive = false, IsFalseNegative = false }
            );

            // === LOGS SEED DATA ===
            modelBuilder.Entity<Log>().HasData(
                new Log { LogId = 1, UserId = 1, ActionType = "Login", Timestamp = new DateTime(2025, 6, 19, 9, 0, 0), Message = "Admin logged into system", Level = "Info" },
                new Log { LogId = 2, UserId = 1, ActionType = "Detection_Review", Timestamp = new DateTime(2025, 6, 19, 11, 0, 0), Message = "Validated tiger detection", Level = "Info", DetectionId = 1 },
                new Log { LogId = 3, UserId = 2, ActionType = "Login", Timestamp = new DateTime(2025, 6, 19, 14, 0, 0), Message = "Zoo Manager logged into system", Level = "Info" },
                new Log { LogId = 4, UserId = 2, ActionType = "Alert_Response", Timestamp = new DateTime(2025, 6, 19, 14, 33, 0), Message = "Responded to elephant detection alert", Level = "Info", DetectionId = 3 },
                new Log { LogId = 5, UserId = 1, ActionType = "System_Maintenance", Timestamp = new DateTime(2025, 6, 20, 8, 0, 0), Message = "Device maintenance scheduled for Central Zone", Level = "Warning" },
                new Log { LogId = 6, UserId = 3, ActionType = "Login", Timestamp = new DateTime(2025, 6, 20, 12, 30, 0), Message = "Wildlife Observer logged into system", Level = "Info" },
                new Log { LogId = 7, UserId = 3, ActionType = "Detection_Review", Timestamp = new DateTime(2025, 6, 20, 13, 0, 0), Message = "Marked detection as false positive", Level = "Info", DetectionId = 7 },
                new Log { LogId = 8, UserId = 1, ActionType = "Emergency_Alert", Timestamp = new DateTime(2025, 6, 20, 15, 2, 20), Message = "Emergency alert triggered for bear sighting", Level = "Critical", DetectionId = 8 }
            );
        }

        // TOMER's OnModelCreating
        /*
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
        */
    }
}

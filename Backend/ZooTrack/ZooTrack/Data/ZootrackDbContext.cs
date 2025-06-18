using Microsoft.EntityFrameworkCore;
using ZooTrack.Models;

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
        public DbSet<Animal> Animals { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        // ADD THIS LINE: DbSet for DetectionValidation
        public DbSet<DetectionValidation> DetectionValidations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure one-to-one relationship between User and UserSettings
            modelBuilder.Entity<UserSettings>()
                .HasOne(us => us.User)
                .WithOne(u => u.UserSettings)
                .HasForeignKey<UserSettings>(us => us.UserId);

            // Additional configuration if needed
            // For example, configuring indexes, unique constraints, etc.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // --- SEED USERS ---
            modelBuilder.Entity<User>().HasData(
                new User { UserId = 1, Name = "Admin", Email = "admin@zootrack.local", Role = "Admin" },
                new User { UserId = 2, Name = "Ranger Rick", Email = "ranger@zootrack.local", Role = "Ranger" },
                new User { UserId = 3, Name = "Zoologist Sarah", Email = "sarah@zootrack.local", Role = "Researcher" },
                new User { UserId = 4, Name = "Guide Tom", Email = "tom@zootrack.local", Role = "Guide" }
            );

            // --- SEED USER SETTINGS (UPDATED) ---
            // Create default target animals JSON string
            var defaultAnimalsJson = System.Text.Json.JsonSerializer.Serialize(new List<string>
            {
                "person", "dog", "cow", "wolf", "tiger", "lion", "elephant", "zebra", "giraffe", "rhino"
            });

            var adminAnimalsJson = System.Text.Json.JsonSerializer.Serialize(new List<string>
            {
                "person", "dog", "cow", "wolf", "tiger", "lion", "elephant", "zebra", "giraffe", "rhino", "leopard", "cheetah"
            });

            var rangerAnimalsJson = System.Text.Json.JsonSerializer.Serialize(new List<string>
            {
                "lion", "elephant", "zebra", "giraffe", "rhino", "leopard", "buffalo"
            });

            var researcherAnimalsJson = System.Text.Json.JsonSerializer.Serialize(new List<string>
            {
                "lion", "elephant", "zebra", "giraffe", "rhino", "leopard", "cheetah", "buffalo", "antelope", "warthog"
            });

            var guideAnimalsJson = System.Text.Json.JsonSerializer.Serialize(new List<string>
            {
                "lion", "elephant", "zebra", "giraffe", "rhino"
            });

            modelBuilder.Entity<UserSettings>().HasData(
                new UserSettings
                {
                    UserId = 1,
                    NotificationPreference = "Email",
                    DetectionThreshold = 0.8f,
                    TargetAnimalsJson = adminAnimalsJson,
                    HighlightSavePath = "Media/HighlightFrames/Admin"
                },
                new UserSettings
                {
                    UserId = 2,
                    NotificationPreference = "SMS",
                    DetectionThreshold = 0.7f,
                    TargetAnimalsJson = rangerAnimalsJson,
                    HighlightSavePath = "Media/HighlightFrames/Ranger"
                },
                new UserSettings
                {
                    UserId = 3,
                    NotificationPreference = "Both",
                    DetectionThreshold = 0.85f,
                    TargetAnimalsJson = researcherAnimalsJson,
                    HighlightSavePath = "Media/HighlightFrames/Researcher"
                },
                new UserSettings
                {
                    UserId = 4,
                    NotificationPreference = "None",
                    DetectionThreshold = 0.6f,
                    TargetAnimalsJson = guideAnimalsJson,
                    HighlightSavePath = "Media/HighlightFrames/Guide"
                }
            );

            // --- SEED DEVICES ---
            modelBuilder.Entity<Device>().HasData(
                new Device { DeviceId = 1, Location = "North Zone - Watering Hole", Status = "Online", LastActive = new DateTime(2025, 01, 01, 12, 0, 0) },
                new Device { DeviceId = 2, Location = "South Zone - Savanna", Status = "Online", LastActive = new DateTime(2025, 01, 01, 11, 30, 0) },
                new Device { DeviceId = 3, Location = "East Zone - Forest Edge", Status = "Offline", LastActive = new DateTime(2024, 12, 30, 9, 0, 0) },
                new Device { DeviceId = 4, Location = "West Zone - River Crossing", Status = "Maintenance", LastActive = new DateTime(2025, 01, 01, 12, 0, 0) }
            );

            // --- SEED EVENTS ---
            modelBuilder.Entity<Event>().HasData(
                new Event { EventId = 1, StartTime = new DateTime(2025, 01, 01, 12, 0, 0), EndTime = new DateTime(2025, 01, 01, 12, 45, 0), Status = "Completed" },
                new Event { EventId = 2, StartTime = new DateTime(2025, 01, 02, 14, 0, 0), EndTime = new DateTime(2025, 01, 02, 16, 0, 0), Status = "Completed" },
                new Event { EventId = 3, StartTime = new DateTime(2025, 01, 03, 10, 0, 0), EndTime = new DateTime(2025, 01, 03, 10, 20, 0), Status = "Completed" },
                new Event { EventId = 4, StartTime = new DateTime(2025, 01, 04, 8, 0, 0), EndTime = new DateTime(2025, 01, 04, 9, 0, 0), Status = "Completed" },
                new Event { EventId = 5, StartTime = new DateTime(2025, 01, 05, 15, 0, 0), EndTime = new DateTime(2025, 01, 05, 15, 30, 0), Status = "Active" }
            );

            // --- SEED MEDIA ---
            modelBuilder.Entity<Media>().HasData(
                new Media { MediaId = 1, Type = "Image", FilePath = "/storage/images/elephant_group_20250423.jpg", Timestamp = new DateTime(2025, 01, 01, 12, 0, 0), DeviceId = 1 },
                new Media { MediaId = 2, Type = "Video", FilePath = "/storage/videos/lion_pride_20250424.mp4", Timestamp = new DateTime(2025, 01, 02, 14, 0, 0), DeviceId = 2 },
                new Media { MediaId = 3, Type = "Image", FilePath = "/storage/images/rhino_watering_20250426.jpg", Timestamp = new DateTime(2025, 01, 03, 10, 0, 0), DeviceId = 1 },
                new Media { MediaId = 4, Type = "Image", FilePath = "/storage/images/giraffe_family_20250427.jpg", Timestamp = new DateTime(2025, 01, 04, 8, 0, 0), DeviceId = 2 },
                new Media { MediaId = 5, Type = "Video", FilePath = "/storage/videos/zebra_crossing_20250428.mp4", Timestamp = new DateTime(2025, 01, 05, 15, 0, 0), DeviceId = 4 }
            );

            // --- SEED DETECTIONS ---
            modelBuilder.Entity<Detection>().HasData(
                new Detection { DetectionId = 1, Confidence = 0.92f, DetectedAt = new DateTime(2025, 01, 01, 12, 0, 0), MediaId = 1, EventId = 1, DeviceId = 1 },
                new Detection { DetectionId = 2, Confidence = 0.85f, DetectedAt = new DateTime(2025, 01, 02, 14, 30, 0), MediaId = 2, EventId = 2, DeviceId = 2 },
                new Detection { DetectionId = 3, Confidence = 0.79f, DetectedAt = new DateTime(2025, 01, 03, 10, 5, 0), MediaId = 3, EventId = 3, DeviceId = 1 },
                new Detection { DetectionId = 4, Confidence = 0.88f, DetectedAt = new DateTime(2025, 01, 04, 8, 30, 0), MediaId = 4, EventId = 4, DeviceId = 2 },
                new Detection { DetectionId = 5, Confidence = 0.75f, DetectedAt = new DateTime(2025, 01, 05, 15, 15, 0), MediaId = 5, EventId = 5, DeviceId = 4 }
            );

            // --- SEED ANIMALS ---
            modelBuilder.Entity<Animal>().HasData(
                new Animal { AnimalId = 1, Species = "African Elephant", ConfidenceLevel = 0.93f, DetectionId = 1 },
                new Animal { AnimalId = 2, Species = "African Elephant", ConfidenceLevel = 0.89f, DetectionId = 1 },
                new Animal { AnimalId = 3, Species = "Lion", ConfidenceLevel = 0.87f, DetectionId = 2 },
                new Animal { AnimalId = 4, Species = "Lion", ConfidenceLevel = 0.82f, DetectionId = 2 },
                new Animal { AnimalId = 5, Species = "Lion", ConfidenceLevel = 0.79f, DetectionId = 2 },
                new Animal { AnimalId = 6, Species = "White Rhino", ConfidenceLevel = 0.91f, DetectionId = 3 },
                new Animal { AnimalId = 7, Species = "Giraffe", ConfidenceLevel = 0.94f, DetectionId = 4 },
                new Animal { AnimalId = 8, Species = "Giraffe", ConfidenceLevel = 0.92f, DetectionId = 4 },
                new Animal { AnimalId = 9, Species = "Zebra", ConfidenceLevel = 0.88f, DetectionId = 5 },
                new Animal { AnimalId = 10, Species = "Zebra", ConfidenceLevel = 0.86f, DetectionId = 5 },
                new Animal { AnimalId = 11, Species = "Zebra", ConfidenceLevel = 0.84f, DetectionId = 5 },
                new Animal { AnimalId = 12, Species = "Zebra", ConfidenceLevel = 0.79f, DetectionId = 5 }
            );

            // --- SEED ALERTS ---
            modelBuilder.Entity<Alert>().HasData(
                new Alert { AlertId = 1, Message = "Elephant herd detected at North Zone watering hole", CreatedAt = new DateTime(2025, 01, 01, 12, 10, 0), DetectionId = 1, UserId = 2 },
                new Alert { AlertId = 2, Message = "Lion pride spotted in South Zone", CreatedAt = new DateTime(2025, 01, 02, 14, 45, 0), DetectionId = 2, UserId = 1 },
                new Alert { AlertId = 3, Message = "Rhino sighting at North Zone watering hole", CreatedAt = new DateTime(2025, 01, 03, 10, 15, 0), DetectionId = 3, UserId = 3 },
                new Alert { AlertId = 4, Message = "Giraffe family detected in South Zone", CreatedAt = new DateTime(2025, 01, 04, 8, 45, 0), DetectionId = 4, UserId = 2 },
                new Alert { AlertId = 5, Message = "Zebra crossing detected at West Zone river", CreatedAt = new DateTime(2025, 01, 05, 15, 25, 0), DetectionId = 5, UserId = 3 }
            );

            // --- SEED LOGS ---
            modelBuilder.Entity<Log>().HasData(
                new Log { LogId = 1, UserId = 1, ActionType = "System Start", Timestamp = new DateTime(2024, 12, 22, 9, 0, 0), Message = "System initialization complete", Level = "Info" },
                new Log { LogId = 2, UserId = 1, ActionType = "User Login", Timestamp = new DateTime(2024, 12, 25, 8, 0, 0), Message = "Admin logged in", Level = "Info" },
                new Log { LogId = 3, UserId = 2, ActionType = "Detection Review", Timestamp = new DateTime(2025, 01, 01, 12, 15, 0), Message = "Ranger Rick reviewed elephant detection", Level = "Info", DetectionId = 1 },
                new Log { LogId = 4, UserId = 3, ActionType = "Settings Change", Timestamp = new DateTime(2025, 01, 01, 13, 0, 0), Message = "Detection threshold updated to 0.85", Level = "Warning" },
                new Log { LogId = 5, UserId = 1, ActionType = "Device Status", Timestamp = new DateTime(2025, 01, 03, 10, 0, 0), Message = "Device East Zone went offline", Level = "Error" },
                new Log { LogId = 6, UserId = 4, ActionType = "Media View", Timestamp = new DateTime(2025, 01, 03, 11, 0, 0), Message = "Guide Tom accessed rhino sighting media", Level = "Info", DetectionId = 3 },
                new Log { LogId = 7, UserId = 2, ActionType = "Alert Configuration", Timestamp = new DateTime(2025, 01, 05, 10, 0, 0), Message = "Added new alert for endangered species", Level = "Info" },
                new Log { LogId = 8, UserId = 1, ActionType = "System Maintenance", Timestamp = new DateTime(2025, 01, 05, 11, 0, 0), Message = "Database backup completed", Level = "Info" }
            );
        }
    }
}
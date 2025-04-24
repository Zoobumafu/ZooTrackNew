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
                new User { UserId = 2, Name = "Ranger Rick", Email = "ranger@zootrack.local", Role = "Ranger" }
            );

            // --- SEED DEVICES ---
            modelBuilder.Entity<Device>().HasData(
                new Device { DeviceId = 1, Location = "North Zone", Status = "Online", LastActive = new DateTime(2025, 4, 24, 14, 0, 0) },
                new Device { DeviceId = 2, Location = "South Zone", Status = "Offline", LastActive = new DateTime(2025, 4, 24, 14, 30, 0) }
            );

            // --- SEED USER SETTINGS ---
            modelBuilder.Entity<UserSettings>().HasData(
                new UserSettings { UserId = 1, NotificationPreference = "Email", DetectionThreshold = 0.8f },
                new UserSettings { UserId = 2, NotificationPreference = "SMS", DetectionThreshold = 0.7f }
            );
        }
    }
}


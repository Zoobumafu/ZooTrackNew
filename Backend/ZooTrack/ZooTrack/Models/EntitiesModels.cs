using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ZooTrack.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }

        // Navigation properties
        public virtual UserSettings UserSettings { get; set; }
        public virtual ICollection<Alert> Alerts { get; set; }
        public virtual ICollection<Log> Logs { get; set; }
    }

    public class Device
    {
        [Key]
        public int DeviceId { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public DateTime LastActive { get; set; }

        // Navigation properties
        public virtual ICollection<Media> Media { get; set; }
    }

    public class Media
    {
        [Key]
        public int MediaId { get; set; }
        public string Type { get; set; }
        public string FilePath { get; set; }
        public DateTime Timestamp { get; set; }

        // Foreign keys
        public int DeviceId { get; set; }

        // Navigation properties
        [ForeignKey("DeviceId")]
        [JsonIgnore]
        public virtual Device Device { get; set; }
        public virtual ICollection<Detection> Detections { get; set; }
    }

    public class Detection
    {
        [Key]
        public int DetectionId { get; set; }
        public float Confidence { get; set; }
        public DateTime DetectedAt { get; set; }

        public int DeviceId { get; set; }
        [JsonIgnore]
        public Device Device { get; set; }

        // Foreign keys
        public int MediaId { get; set; }
        public int EventId { get; set; }

        // Navigation properties
        [ForeignKey("MediaId")]
        [JsonIgnore]
        public virtual Media Media { get; set; }

        [ForeignKey("EventId")]
        [JsonIgnore]
        public virtual Event Event { get; set; }

        public virtual ICollection<Animal> Animals { get; set; }
        public virtual ICollection<Alert> Alerts { get; set; }
    }

    public class Animal
    {
        [Key]
        public int AnimalId { get; set; }
        public string Species { get; set; }
        public float ConfidenceLevel { get; set; }

        // Foreign keys
        public int DetectionId { get; set; }

        // Navigation properties
        [ForeignKey("DetectionId")]
        [JsonIgnore]
        public virtual Detection Detection { get; set; }
    }

    public class Alert
    {
        [Key]
        public int AlertId { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }

        // Foreign keys
        public int DetectionId { get; set; }
        public int UserId { get; set; }

        // Navigation properties
        [ForeignKey("DetectionId")]
        [JsonIgnore]
        public virtual Detection Detection { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual User User { get; set; }
    }

    public class Event
    {
        [Key]
        public int EventId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }

        // Navigation properties
        public virtual ICollection<Detection> Detections { get; set; }
    }

    public class Log
    {
        [Key]
        public int LogId { get; set; }
        [Required]
        public int UserId { get; set; }
        [Required]
        [MaxLength(100)]
        public string ActionType { get; set; }
        [Required]
        public DateTime Timestamp { get; set; }

        [MaxLength(500)]
        public string Message { get; set; }
        [MaxLength(20)]
        public string Level { get; set; } = "Info";
        public int? DetectionId { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual User User { get; set; }

        [ForeignKey("DetectionId")]
        [JsonIgnore]
        public virtual Detection? Detection { get; set; }
    }

    public class UserSettings
    {
        [Key]
        public int UserId { get; set; }
        public string? NotificationPreference { get; set; }
        public float DetectionThreshold { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual User User { get; set; }
    }


}

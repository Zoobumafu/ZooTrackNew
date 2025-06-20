using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Collections.Generic;
using System;

namespace ZooTrack.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string Name { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Role { get; set; }

        // --- These properties are required for authentication ---
        [JsonIgnore]
        public string PasswordHash { get; set; }
        [JsonIgnore]
        public string PasswordSalt { get; set; }


        // Navigation properties
        public virtual UserSettings UserSettings { get; set; }
        public virtual ICollection<Alert> Alerts { get; set; }
        public virtual ICollection<Log> Logs { get; set; }
    }

    // Other models remain the same...
    public class Device
    {
        [Key]
        public int DeviceId { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public DateTime LastActive { get; set; }
        public virtual ICollection<Media> Media { get; set; }
    }

    public class Media
    {
        [Key]
        public int MediaId { get; set; }
        public string Type { get; set; }
        public string FilePath { get; set; }
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
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
        public int? TrackingId { get; set; }
        public float BoundingBoxX { get; set; }
        public float BoundingBoxY { get; set; }
        public float BoundingBoxWidth { get; set; }
        public float BoundingBoxHeight { get; set; }
        public int FrameNumber { get; set; }
        public string? DetectedObject { get; set; }
        public int MediaId { get; set; }
        public int EventId { get; set; }
        [ForeignKey("MediaId")]
        [JsonIgnore]
        public virtual Media Media { get; set; }
        [ForeignKey("EventId")]
        [JsonIgnore]
        public virtual Event Event { get; set; }
        public virtual ICollection<Animal> Animals { get; set; }
        public virtual ICollection<Alert> Alerts { get; set; }
    }

    public class DetectionValidation
    {
        [Key]
        public int Id { get; set; }
        public int DetectionId { get; set; }
        [ForeignKey("DetectionId")]
        [JsonIgnore]
        public Detection Detection { get; set; }
        public bool IsValidated { get; set; }
        public bool IsTruePositive { get; set; }
        public string? ValidationNotes { get; set; }
        public DateTime ValidatedAt { get; set; }
        public string ValidatedBy { get; set; }
        public bool IsFalsePositive { get; set; }
        public bool IsFalseNegative { get; set; }
    }

    public class Animal
    {
        [Key]
        public int AnimalId { get; set; }
        public string Species { get; set; }
        public float ConfidenceLevel { get; set; }
        public int DetectionId { get; set; }
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
        public int DetectionId { get; set; }
        public int UserId { get; set; }
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
        public float DetectionThreshold { get; set; }
        public string? NotificationPreference { get; set; }
        public string TargetAnimalsJson { get; set; } = "[]";
        public string HighlightSavePath { get; set; } = string.Empty;
        [NotMapped]
        public List<string> TargetAnimals
        {
            get => string.IsNullOrEmpty(TargetAnimalsJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(TargetAnimalsJson) ?? new List<string>();
            set => TargetAnimalsJson = JsonSerializer.Serialize(value ?? new List<string>());
        }
        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual User User { get; set; }
    }
}

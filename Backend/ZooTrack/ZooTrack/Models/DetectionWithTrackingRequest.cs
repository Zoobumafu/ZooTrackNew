using System;

namespace ZooTrack.Models
{
    public class DetectionWithTrackingRequest
    {
        public float Confidence { get; set; }
        public int DeviceId { get; set; }
        public int MediaId { get; set; }
        public int EventId { get; set; }
        public DateTime? DetectedAt { get; set; }

        // Bounding box coordinates (normalized 0-1)
        public float BoundingBoxX { get; set; }
        public float BoundingBoxY { get; set; }
        public float BoundingBoxWidth { get; set; }
        public float BoundingBoxHeight { get; set; }

        public string? DetectedObject { get; set; }
    }
}

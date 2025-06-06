// Client Project: Models/AlertSettingViewModel.cs
// Ensure the namespace matches your project structure, e.g., ZooTrack.Client.Models

namespace ZooTrack.Client
{
    public class AlertSettingViewModel
    {
        /// <summary>
        /// A unique identifier for this specific alert setting instance on the client.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The ID of the animal selected for this alert.
        /// This would correspond to Animal.AnimalId from your backend.
        /// </summary>
        public int AnimalId { get; set; }

        /// <summary>
        /// The name of the selected animal (for display purposes).
        /// This can be populated when the animal is selected from the dropdown.
        /// </summary>
        public string AnimalName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the MP3 file selected by the user.
        /// </summary>
        public string? Mp3FileName { get; set; }

        /// <summary>
        /// Optional: To store the MP3 file content as a byte array.
        /// This would be used if you intend to upload the file to a server
        /// or handle playback directly with the byte data via JavaScript interop.
        /// For now, it's commented out as we focused on file name selection.
        /// </summary>
        // public byte[]? Mp3FileContent { get; set; }

        // Consider adding:
        // public int AssociatedDeviceId { get; set; } // If you need to explicitly link this setting back to a device ID,
        // though it will be part of a list within a StreamComponentConfig
        // which already has a SelectedDeviceId.
    }
}

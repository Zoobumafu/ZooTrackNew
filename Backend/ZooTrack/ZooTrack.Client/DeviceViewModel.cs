// Shared or Client Project: Models/DeviceViewModel.cs
// This is a simple class to represent device data fetched from your API
// for the dropdown list. Adjust properties as needed based on your actual Device model.
namespace ZooTrack // Assuming a Client project structure
{
    public class DeviceViewModel
    {
        public int DeviceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        // Add any other properties from your backend Device model you want to display or use
    }
}

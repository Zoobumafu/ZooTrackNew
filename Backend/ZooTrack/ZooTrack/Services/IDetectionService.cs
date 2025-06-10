using System.Collections.Generic;
using System.Threading.Tasks;
using ZooTrack.Models;

namespace ZooTrack.Services
{
    public interface IDetectionService
    {
        Task<Detection> CreateDetectionAsync(Detection detection);
        Task<IEnumerable<Detection>> GetDetectionsForDeviceAsync(int deviceId);
        Task<Detection> CreateDetectionWithTrackingAsync(
            Detection detection,
            float boundingBoxX,
            float boundingBoxY,
            float boundingBoxWidth,
            float boundingBoxHeight,
            string detectedObject = null
        );
    }
}

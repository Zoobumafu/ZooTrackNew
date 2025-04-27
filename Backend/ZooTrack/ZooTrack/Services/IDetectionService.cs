using System.Collections.Generic;
using System.Threading.Tasks;
using ZooTrack.Models;

namespace ZooTrack.Services
{
    public interface IDetectionService
    {
        Task<Detection> CreateDetectionAsync(Detection detection);
        Task<IEnumerable<Detection>> GetDetectionsForDeviceAsync(int deviceId);

    }
}

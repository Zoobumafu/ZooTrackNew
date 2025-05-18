using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using ZooTrack.Models;

namespace ZooTrackBackend.Services
{
    /// <summary>
    ///     Proper service abstraction
    ///     Adding filtering and pagination capabilities
    ///     Centralized logging functionality
    /// </summary>

    public interface ILogService
    {
        Task<Log> AddLogAsync(int userId, string actionType, string message = "", string level = "Info", int? detectionId = null);
        Task<IEnumerable<Log>> GetLogsAsync(int? userId = null, string actionType = null,
            DateTime? startDate = null, DateTime? endDate = null, string level = null,
            int? detectionId = null, int pageNumber = 1, int pageSize = 50);
        Task<Log> GetLogByIdAsync(int logId);
        Task<bool> DeleteLogAsync(int logId);
    }
}

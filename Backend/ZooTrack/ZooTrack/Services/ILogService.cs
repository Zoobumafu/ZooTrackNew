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
        Task<Log> AddLogAsync(int? userId, string actionType, string message = "", string level = "Info", int? detectionId = null);

        Task<PaginatedLogResponse> GetLogsAsync(
            int? userId = null,
            string actionType = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string level = null,
            int? detectionId = null,
            int pageNumber = 1,
            int pageSize = 50);

        Task<Log> GetLogByIdAsync(int logId);
        Task<bool> DeleteLogAsync(int logId);
    }

    /// <summary>
    /// Response model for paginated log results
    /// </summary>
    public class PaginatedLogResponse
    {
        public List<Log> Logs { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; }
    }
}

using System.Threading.Tasks;

namespace ZooTrackBackend.Services
{
    public interface IAuthService
    {
        Task<string> Login(string email, string password);
        Task<bool> Register(string username, string email, string password);

        Task<bool> ChangePassword(int userId, string oldPassword, string newPassword);
    }
}

using System.Threading.Tasks;

namespace ZooTrack.Services
{
    public interface IAuthService
    {
        Task<string> Login(string email, string password);
        Task<bool> ChangePassword(int userId, string oldPassword, string newPassword);
    }
}

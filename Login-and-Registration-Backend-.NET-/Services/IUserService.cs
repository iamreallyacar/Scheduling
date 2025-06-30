using Microsoft.AspNetCore.Identity;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Services
{
    public interface IUserService
    {
        Task<ServiceResult<ApplicationUser>> GetUserByIdAsync(string id);
        Task<ServiceResult<ApplicationUser>> GetUserByEmailAsync(string email);
        Task<ServiceResult<ApplicationUser>> GetUserByUsernameAsync(string username);
        Task<ServiceResult<ApplicationUser>> RegisterUserAsync(RegisterRequest request);
        Task<ServiceResult<ApplicationUser>> ValidateUserAsync(LoginRequest request);
        string HashPassword(ApplicationUser user, string password);
        bool VerifyPassword(ApplicationUser user, string providedPassword, string hashedPassword);
    }
}
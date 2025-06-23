using Microsoft.AspNetCore.Identity;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Services
{
    public interface IUserService
    {
        string GenerateJwtToken(ApplicationUser user);
        Task<ApplicationUser?> GetUserByIdAsync(string id);
        Task<ApplicationUser?> GetUserByEmailAsync(string email);
        Task<ApplicationUser?> GetUserByUsernameAsync(string username);
        Task<IdentityResult> RegisterUserAsync(RegisterRequest request);
        Task<(bool Success, ApplicationUser? User)> ValidateUserAsync(LoginRequest request);
        string HashPassword(ApplicationUser user, string password);
        bool VerifyPassword(ApplicationUser user, string providedPassword, string hashedPassword);
    }
}
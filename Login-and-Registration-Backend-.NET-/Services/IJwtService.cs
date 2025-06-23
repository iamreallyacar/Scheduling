using System.Security.Claims;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Services
{
    public interface IJwtService
    {
        string GenerateJwtToken(ApplicationUser user);
        ClaimsPrincipal? ValidateToken(string token);
    }
}
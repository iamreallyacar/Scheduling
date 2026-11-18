using Microsoft.AspNetCore.Identity;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Auth;

// The sign-in name (UserName) is always the email address.
internal sealed class User : IdentityUser<int>
{
    public const int NameMaxLength = 200;

    public required string Name { get; set; }

    public required UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;
}

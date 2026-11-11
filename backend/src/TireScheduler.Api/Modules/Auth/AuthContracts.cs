using System.ComponentModel.DataAnnotations;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Auth;

public sealed record SignInRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

// CanPlan and CanAdminister tell the UI which actions to show, so it does not repeat the
// role rules. Only the API check counts (ARCHITECTURE.md section 6).
public sealed record CurrentUserResponse(string Name, string Email, UserRole Role, bool CanPlan, bool CanAdminister);

// The client sends `Token` back in the header named `HeaderName` on every state-changing request.
public sealed record AntiforgeryTokenResponse(string HeaderName, string Token);

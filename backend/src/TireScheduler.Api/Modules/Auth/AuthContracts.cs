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
// role rules. Only the API check counts (ARCHITECTURE.md section "Access rules").
public sealed record CurrentUserResponse
{
    public required string Name { get; init; }

    public required string Email { get; init; }

    public required UserRole Role { get; init; }

    public required bool CanPlan { get; init; }

    public required bool CanAdminister { get; init; }
}

// The client sends `Token` back in the header named `HeaderName` on every state-changing request.
public sealed record AntiforgeryTokenResponse
{
    public required string HeaderName { get; init; }

    public required string Token { get; init; }
}

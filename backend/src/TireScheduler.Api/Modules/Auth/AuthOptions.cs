namespace TireScheduler.Api.Modules.Auth;

// Settings under "Auth" (README.md, section Configuration).
internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public double SessionHours { get; init; }

    public LockoutSettings Lockout { get; init; } = new();

    public FirstAdminSettings FirstAdmin { get; init; } = new();

    internal sealed class LockoutSettings
    {
        public int MaxFailedAttempts { get; init; }

        public double Minutes { get; init; }
    }

    internal sealed class FirstAdminSettings
    {
        public string? Email { get; init; }

        public string? Password { get; init; }
    }
}

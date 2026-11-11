using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Database;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Auth;

// Creates the first Admin from configuration while no user exists (AC-8). Later users are
// created by an Admin (F-AUTH-3).
internal sealed class FirstAdminStep(AuthDbContext context, UserManager<User> userManager, IOptions<AuthOptions> authOptions) : IStartupStep
{
    private const string EmailSetting = $"{AuthOptions.SectionName}:{nameof(AuthOptions.FirstAdmin)}:{nameof(AuthOptions.FirstAdminSettings.Email)}";
    private const string PasswordSetting = $"{AuthOptions.SectionName}:{nameof(AuthOptions.FirstAdmin)}:{nameof(AuthOptions.FirstAdminSettings.Password)}";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        AuthOptions.FirstAdminSettings settings = authOptions.Value.FirstAdmin;
        string[] missingSettings = new[] { (EmailSetting, settings.Email), (PasswordSetting, settings.Password) }
            .Where(setting => string.IsNullOrWhiteSpace(setting.Item2))
            .Select(setting => setting.Item1)
            .ToArray();
        if (missingSettings.Length > 0 || settings.Email is null || settings.Password is null)
        {
            throw new InvalidOperationException(
                $"No user exists yet and the first Admin cannot be created. Missing settings: {string.Join(", ", missingSettings)} "
                + "(as environment variables, replace ':' with '__').");
        }

        User firstAdmin = new() { UserName = settings.Email, Email = settings.Email, Name = "Administrator", Role = UserRole.Admin };
        IdentityResult result = await userManager.CreateAsync(firstAdmin, settings.Password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"The first Admin could not be created from {EmailSetting} and {PasswordSetting}: {result}");
        }
    }
}

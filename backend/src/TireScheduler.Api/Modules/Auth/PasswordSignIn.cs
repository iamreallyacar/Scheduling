using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.Auth;

// Checks an email and password (AC-1 to AC-4). The lockout is counted here, not by Identity's
// own lockout, because that reads the system clock instead of the injected TimeProvider.
internal sealed class PasswordSignIn(UserManager<User> userManager, TimeProvider timeProvider, IOptions<AuthOptions> authOptions)
{
    public async Task<User> VerifyAsync(string email, string password)
    {
        User? user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        // Unknown email, inactive user and wrong password all get the same answer, so the
        // answer does not reveal which emails exist.
        if (user is null || !user.IsActive)
        {
            throw InvalidCredentials();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (user.LockoutEnd > now)
        {
            throw ApiProblemException.Rejected(
                StatusCodes.Status401Unauthorized,
                ProblemCodes.AuthLockedOut,
                "Too many failed sign-in attempts. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            await CountFailedAttemptAsync(user, now).ConfigureAwait(false);
            throw InvalidCredentials();
        }

        if (user.AccessFailedCount != 0 || user.LockoutEnd is not null)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await SaveAsync(user).ConfigureAwait(false);
        }

        return user;
    }

    private async Task CountFailedAttemptAsync(User user, DateTimeOffset now)
    {
        AuthOptions.LockoutSettings lockout = authOptions.Value.Lockout;
        user.AccessFailedCount++;
        if (user.AccessFailedCount >= lockout.MaxFailedAttempts)
        {
            user.LockoutEnd = now.AddMinutes(lockout.Minutes);
            user.AccessFailedCount = 0;
        }

        await SaveAsync(user).ConfigureAwait(false);
    }

    private async Task SaveAsync(User user)
    {
        IdentityResult result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not save sign-in state of user {user.Id}: {result}");
        }
    }

    private static ApiProblemException InvalidCredentials() => ApiProblemException.Rejected(
        StatusCodes.Status401Unauthorized,
        ProblemCodes.AuthInvalidCredentials,
        "The email or password is not correct.");
}

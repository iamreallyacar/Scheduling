using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.Auth;

// Checks an email and password (AC-1 to AC-4). The lockout is counted here, not by Identity's
// own lockout, because that reads the system clock instead of the injected TimeProvider.
internal sealed class PasswordSignIn(
    UserManager<User> userManager,
    AuthDbContext context,
    RequestTransaction requestTransaction,
    TimeProvider timeProvider,
    IOptions<AuthOptions> authOptions)
{
    public async Task<User> VerifyAsync(string email, string password, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByEmailAsync(email);
        // Unknown email, inactive user and wrong password all get the same answer, so the
        // answer does not reveal which emails exist.
        if (user is null)
        {
            throw InvalidCredentials();
        }

        // Sign-ins of one user run one after the other, so a failure is counted on the count the
        // attempt before it saved, and an attempt that waited sees the lockout that one set (AC-224, DEC-68).
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        if (!await context.LockRowAsync<User>(user.Id, cancellationToken))
        {
            throw InvalidCredentials();
        }

        await context.Entry(user).ReloadAsync(cancellationToken);
        if (!user.IsActive)
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

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await CountFailedAttemptAsync(user, now);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidCredentials();
        }

        if (user.AccessFailedCount != 0 || user.LockoutEnd is not null)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await SaveAsync(user);
        }

        await transaction.CommitAsync(cancellationToken);
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

        await SaveAsync(user);
    }

    private async Task SaveAsync(User user)
    {
        IdentityResult result = await userManager.UpdateAsync(user);
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Auth;

// The session cookie of DEC-21 and ARCHITECTURE.md section 6.
internal static class SessionCookie
{
    public const string Scheme = "Session";

    public static void Configure(CookieAuthenticationOptions cookie, AuthOptions authOptions, CookieSecurityOptions cookieSecurity)
    {
        cookie.Cookie.Name = "tire-scheduler-session";
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SecurePolicy = cookieSecurity.SecurePolicy;
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        cookie.ExpireTimeSpan = TimeSpan.FromHours(authOptions.SessionHours);
        cookie.SlidingExpiration = true;

        // An API answers with a status, not with a redirect to a sign-in page. The body is
        // written by the error handling (ApiProblems.ForStatus).
        cookie.Events.OnRedirectToLogin = context => SetStatus(context, StatusCodes.Status401Unauthorized);
        cookie.Events.OnRedirectToAccessDenied = context => SetStatus(context, StatusCodes.Status403Forbidden);
        cookie.Events.OnValidatePrincipal = RejectEndedSessionAsync;
    }

    private static Task SetStatus(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    // A cookie alone stays valid until it expires. Checking the user's security stamp on every
    // request ends a session at sign-out (AC-5) and when the user is deactivated.
    private static async Task RejectEndedSessionAsync(CookieValidatePrincipalContext context)
    {
        IServiceProvider services = context.HttpContext.RequestServices;
        ClaimsPrincipal principal = context.Principal ?? throw new InvalidOperationException("A validated session has no principal.");
        string securityStampClaimType = services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType;

        User? user = await services.GetRequiredService<UserManager<User>>().GetUserAsync(principal).ConfigureAwait(false);
        if (user is null || !user.IsActive || user.SecurityStamp != principal.FindFirstValue(securityStampClaimType))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme).ConfigureAwait(false);
        }
    }
}

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Database;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Auth;

// Registration point for the Auth module (ARCHITECTURE.md sections 2 and 6): users, sign-in and
// the session. The access policies themselves are in Security, because every module names them.
public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .Validate(options => options.SessionHours > 0, $"Setting {AuthOptions.SectionName}:{nameof(AuthOptions.SessionHours)} must be more than 0.")
            .Validate(
                options => options.Lockout is { MaxFailedAttempts: > 0, Minutes: > 0 },
                $"Settings {AuthOptions.SectionName}:Lockout:MaxFailedAttempts and {AuthOptions.SectionName}:Lockout:Minutes must be more than 0.")
            .ValidateOnStart();

        services.AddModuleDbContext<AuthDbContext>(AuthDbContext.Schema);
        services.AddIdentityCore<User>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddClaimsPrincipalFactory<UserPrincipalFactory>();
        services.AddScoped<PasswordSignIn>();
        // Registered after the Auth migration step, so the user table exists when it runs.
        services.AddScoped<IStartupStep, FirstAdminStep>();

        services.AddAuthentication(SessionCookie.Scheme).AddCookie(SessionCookie.Scheme);
        services.AddOptions<CookieAuthenticationOptions>(SessionCookie.Scheme)
            .Configure<IOptions<AuthOptions>, IOptions<CookieSecurityOptions>>((cookie, authOptions, cookieSecurity) =>
                SessionCookie.Configure(cookie, authOptions.Value, cookieSecurity.Value));

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        AuthEndpoints.Map(endpoints);
        return endpoints;
    }
}

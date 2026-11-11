using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TireScheduler.Api.Modules.Auth;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests;

public sealed class SchedulerApiFactory(
    string? connectionString,
    bool withFirstAdminSettings = true,
    TimeProvider? clock = null,
    bool cookiesRequireHttps = true)
    : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@plant.test";
    public const string AdminPassword = "First-Admin-1";
    public const string UserPassword = "Some-User-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:Scheduler", connectionString);
        }

        if (withFirstAdminSettings)
        {
            builder.UseSetting("Auth:FirstAdmin:Email", AdminEmail);
            builder.UseSetting("Auth:FirstAdmin:Password", AdminPassword);
        }

        builder.UseSetting("Security:CookiesRequireHttps", cookiesRequireHttps.ToString());

        if (clock is not null)
        {
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(clock)));
        }
    }

    // A browser-like client with no session yet. https by default, as in production, where
    // the cookies are marked Secure.
    internal ApiSession NewSession(bool overHttps = true) =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(overHttps ? "https://localhost" : "http://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        }));

    internal async Task<ApiSession> SignInAsync(string email, string password)
    {
        ApiSession session = NewSession();
        HttpResponseMessage response = await session.SignInAsync(email, password);
        Assert.True(response.IsSuccessStatusCode, $"Sign-in of {email} failed with {response.StatusCode}.");
        return session;
    }

    // Signs in as the first Admin, or as a user of the given role that is created when missing.
    internal async Task<ApiSession> SignInAsRoleAsync(UserRole role)
    {
        if (role == UserRole.Admin)
        {
            return await SignInAsync(AdminEmail, AdminPassword);
        }

        string email = $"test-{role}@plant.test";
        await EnsureUserAsync(email, role);
        return await SignInAsync(email, UserPassword);
    }

    // There is no user management API in v1 (F-AUTH-3 is P1), so tests create users directly.
    internal async Task EnsureUserAsync(string email, UserRole role, bool isActive = true)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        User user = new() { UserName = email, Email = email, Name = $"Test {role}", Role = role, IsActive = isActive };
        IdentityResult result = await userManager.CreateAsync(user, UserPassword);
        Assert.True(result.Succeeded, result.ToString());
    }
}

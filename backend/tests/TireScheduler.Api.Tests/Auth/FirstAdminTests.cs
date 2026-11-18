using System.Text.Json;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.Auth;

public sealed class FirstAdminTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_8_first_admin_is_created_from_settings_when_no_user_exists_and_settings_are_ignored_afterwards()
    {
        string connectionString = postgres.NewDatabaseConnectionString();
        await using (SchedulerApiFactory firstStart = new(connectionString))
        {
            using ApiSession session = await firstStart.SignInAsRoleAsync(UserRole.Admin);
            JsonElement me = await session.GetJsonAsync("/api/v1/me");
            Assert.Equal("Admin", me.GetProperty("role").GetString());
            Assert.Equal(SchedulerApiFactory.AdminEmail, me.GetProperty("email").GetString());
        }

        // Users exist now, so a start without the settings must not fail.
        await using SchedulerApiFactory secondStart = new(connectionString, withFirstAdminSettings: false);
        using ApiSession sessionAfterRestart = await secondStart.SignInAsRoleAsync(UserRole.Admin);
    }

    [Fact]
    public async Task AC_8_startup_fails_naming_the_missing_settings_when_no_user_exists()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), withFirstAdminSettings: false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Auth:FirstAdmin:Email", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Auth:FirstAdmin:Password", exception.Message, StringComparison.Ordinal);
    }
}

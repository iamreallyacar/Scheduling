using System.Text.Json;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.MasterData;

// AC-41: the plant time zone comes from configuration (A18) and is checked at startup.
public sealed class PlantTimeZoneTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_41_every_calendar_uses_the_configured_plant_time_zone()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), plantTimeZone: "America/Chicago");
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement calendars = await viewer.GetJsonAsync("/api/v1/calendars");

        Assert.NotEmpty(calendars.EnumerateArray());
        Assert.All(calendars.EnumerateArray(), calendar => Assert.Equal("America/Chicago", calendar.GetProperty("timeZone").GetString()));
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    // A Windows zone id is known to .NET on every platform, but it is not an IANA id.
    [InlineData("W. Europe Standard Time")]
    public async Task AC_41_startup_stops_with_an_error_naming_a_value_that_is_not_an_iana_zone_id(string configuredZone)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), plantTimeZone: configuredZone);

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains($"'{configuredZone}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Plant:TimeZone", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC_41_startup_stops_with_an_error_naming_the_setting_when_it_is_missing()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), plantTimeZone: null);

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Plant:TimeZone", exception.Message, StringComparison.Ordinal);
    }
}

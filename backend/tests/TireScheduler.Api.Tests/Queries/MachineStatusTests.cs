using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Queries;

// F-MNT-2 through the machine reads of Queries (DEC-39), on a fixed clock. Running needs lot
// progress and follows in task M5-1.
public sealed class MachineStatusTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = Berlin(17, 10);

    private readonly FakeTimeProvider clock = new(Now);
    private readonly SchedulerApiFixture api;

    public MachineStatusTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_172_status_follows_the_unavailabilities_that_cover_now_in_the_list_and_the_single_read()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "Curing");
        int brokenId = await CreateResourceAsync(admin, "Press 1", typeId);
        int maintainedId = await CreateResourceAsync(admin, "Press 2", typeId);
        int brokenDuringMaintenanceId = await CreateResourceAsync(admin, "Press 3", typeId);
        int idleId = await CreateResourceAsync(admin, "Press 4", typeId);
        int endedJustNowId = await CreateResourceAsync(admin, "Press 5", typeId);
        int startsLaterId = await CreateResourceAsync(admin, "Press 6", typeId);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await CreateUnavailabilityAsync(planner, brokenId, Unavailability("Breakdown", Now.AddDays(-2), end: null));
        await CreateUnavailabilityAsync(planner, maintainedId, Unavailability("Planned", Now, Now.AddHours(1)));
        await CreateUnavailabilityAsync(planner, brokenDuringMaintenanceId, Unavailability("Planned", Now.AddHours(-1), Now.AddHours(1)));
        await CreateUnavailabilityAsync(planner, brokenDuringMaintenanceId, Unavailability("Breakdown", Now.AddMinutes(-30), Now.AddMinutes(30)));
        await CreateUnavailabilityAsync(planner, endedJustNowId, Unavailability("Breakdown", Now.AddHours(-3), Now));
        await CreateUnavailabilityAsync(planner, startsLaterId, Unavailability("Planned", Now.AddMinutes(1), Now.AddHours(2)));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement listed = await viewer.GetJsonAsync(ResourcesPath);

        Dictionary<int, string> statusById = listed.EnumerateArray().ToDictionary(item => item.GetProperty("id").GetInt32(), item => item.GetProperty("status").GetString()!);
        Assert.Equal("Down", statusById[brokenId]);
        Assert.Equal("Maintenance", statusById[maintainedId]);
        Assert.Equal("Down", statusById[brokenDuringMaintenanceId]);
        Assert.Equal("Idle", statusById[idleId]);
        Assert.Equal("Idle", statusById[endedJustNowId]);
        Assert.Equal("Idle", statusById[startsLaterId]);
        Assert.Equal("Down", (await viewer.GetJsonAsync($"{ResourcesPath}/{brokenId}")).GetProperty("status").GetString());
        Assert.Equal("Idle", (await viewer.GetJsonAsync($"{ResourcesPath}/{startsLaterId}")).GetProperty("status").GetString());

        clock.SetUtcNow(Now.AddMinutes(1));

        Assert.Equal("Maintenance", (await viewer.GetJsonAsync($"{ResourcesPath}/{startsLaterId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC_172_the_status_comes_with_the_machine_fields_and_the_filters_still_apply()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int curingId = await CreateResourceTypeAsync(admin, "Curing");
        int buildingId = await CreateResourceTypeAsync(admin, "Building");
        int pressId = await CreateResourceAsync(admin, "Press 1", curingId);
        await CreateResourceAsync(admin, "Builder 1", buildingId);
        await CreateUnavailabilityAsync(admin, pressId, Unavailability("Breakdown", Now.AddHours(-1), end: null));

        JsonElement curingOnly = await admin.GetJsonAsync($"{ResourcesPath}?resourceTypeId={curingId}&isActive=true");

        JsonElement press = Assert.Single(curingOnly.EnumerateArray());
        Assert.Equal("Press 1", press.GetProperty("name").GetString());
        Assert.Equal("Curing", press.GetProperty("resourceType").GetProperty("name").GetString());
        Assert.Equal("Plant default", press.GetProperty("calendar").GetProperty("name").GetString());
        Assert.True(press.GetProperty("isActive").GetBoolean());
        Assert.Equal("Down", press.GetProperty("status").GetString());
    }
}

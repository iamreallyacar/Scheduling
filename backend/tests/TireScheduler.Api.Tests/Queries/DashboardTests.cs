using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.ProgressTestData;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Queries;

// F-RPT-1 (acceptance-criteria.md section 9): the planner dashboard of the live version, on a fixed
// clock in Europe/Berlin.
public sealed class DashboardTests : IAsyncDisposable
{
    // Wed 2026-11-18, so today is 2026-11-18 and the due-soon days run to 2026-11-25 (A49).
    private static readonly DateTimeOffset Now = Berlin(18, 10);

    private readonly FakeTimeProvider clock = new(Now);
    private readonly SchedulerApiFixture api;

    public DashboardTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private static string DashboardPath(string versionId) => $"/api/v1/versions/{versionId}/dashboard";

    [Fact]
    public async Task AC_218_lists_the_unfinished_orders_due_before_today_and_due_within_seven_days_by_due_date_and_order_number()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC218");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await CreateOrderAsync(planner, NewOrder("SO-2", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-17"));
        int[] inProgressLots = LotIds(await CreateOrderAsync(planner, NewOrder("SO-1", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-10")));
        await PlaceLotAsync(planner, inProgressLots[0], plant.Builder1, Berlin(16, 12));
        await RecordProgressAsync(planner, inProgressLots[0], Berlin(16, 12));
        int[] completedLots = LotIds(await CreateOrderAsync(planner, NewOrder("SO-COMPLETED", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-17")));
        await PlaceLotAsync(planner, completedLots[0], plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, completedLots[1], plant.Press1, Berlin(16, 10));
        await RecordProgressAsync(planner, completedLots[0], Berlin(16, 8), Berlin(16, 10));
        await RecordProgressAsync(planner, completedLots[1], Berlin(16, 10), Berlin(16, 14));
        await CancelOrderAsync(planner, (await CreateOrderAsync(planner, NewOrder("SO-CANCELLED", plant.ProductId, dueDate: "2026-11-17"))).GetProperty("id").GetInt32());
        int deletedId = (await CreateOrderAsync(planner, NewOrder("SO-DELETED", plant.ProductId, dueDate: "2026-11-18"))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await planner.DeleteAsync(OrderPath(deletedId))).StatusCode);
        // Created before SO-4, so the list follows the order number, not the creation.
        await CreateOrderAsync(planner, NewOrder("SO-5", plant.ProductId, dueDate: "2026-11-25"));
        JsonElement dueTodayPlannedLate = await CreateOrderAsync(planner, NewOrder("SO-3", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-18", customerName: "Fleet Service"));
        // Ends Thu 10:00, after the end of the due day.
        await PlaceLotAsync(planner, LotIds(dueTodayPlannedLate)[0], plant.Builder1, Berlin(19, 8));
        await CreateOrderAsync(planner, NewOrder("SO-4", plant.ProductId, dueDate: "2026-11-25"));
        await CreateOrderAsync(planner, NewOrder("SO-LATER", plant.ProductId, dueDate: "2026-11-26"));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement dashboard = await viewer.GetJsonAsync(DashboardPath(LiveVersion));

        Assert.Equal("2026-11-25", dashboard.GetProperty("dueSoonUntil").GetString());
        Assert.Equal(
            [("SO-1", "2026-11-10", "InProgress", true), ("SO-2", "2026-11-17", "Pending", true)],
            OrderValues(dashboard.GetProperty("overdueOrders")));
        Assert.Equal(
            [("SO-3", "2026-11-18", "Scheduled", true), ("SO-4", "2026-11-25", "Pending", false), ("SO-5", "2026-11-25", "Pending", false)],
            OrderValues(dashboard.GetProperty("dueSoonOrders")));
        JsonElement listed = dashboard.GetProperty("dueSoonOrders")[0];
        Assert.Equal(dueTodayPlannedLate.GetProperty("id").GetInt32(), listed.GetProperty("id").GetInt32());
        Assert.Equal("Fleet Service", listed.GetProperty("customerName").GetString());
        Assert.Equal("AC218", listed.GetProperty("product").GetProperty("code").GetString());
        Assert.Equal(LotQuantity, listed.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task AC_218_the_status_and_late_flag_follow_the_placements_of_the_version_read()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC218V");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int lotId = LotIds(await CreateOrderAsync(planner, NewOrder("SO-218", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-18")))[0];
        // Placed only in a scenario, ending Thu 10:00, after the end of the due day.
        int scenarioId = await AddScenarioPlacementAsync(api.Factory, lotId, plant.Builder1, Berlin(19, 8), Berlin(19, 10));

        JsonElement live = await planner.GetJsonAsync(DashboardPath(LiveVersion));
        JsonElement scenario = await planner.GetJsonAsync(DashboardPath(scenarioId.ToString(CultureInfo.InvariantCulture)));

        Assert.Equal([("SO-218", "2026-11-18", "Pending", false)], OrderValues(live.GetProperty("dueSoonOrders")));
        Assert.Equal([("SO-218", "2026-11-18", "Scheduled", true)], OrderValues(scenario.GetProperty("dueSoonOrders")));
    }

    [Fact]
    public async Task AC_218_AC_23_a_version_that_does_not_exist_is_not_found()
    {
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage response = await viewer.GetAsync(DashboardPath("999"));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_219_an_order_turns_from_due_soon_to_overdue_at_plant_local_midnight()
    {
        // 22:59Z, the last minute of Monday in Berlin. Set before signing in, because a session ends
        // after 12 hours without a request (A21).
        clock.SetUtcNow(Berlin(23, 23, 59));
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC219");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await CreateOrderAsync(planner, NewOrder("SO-219", plant.ProductId, dueDate: "2026-11-23"));

        JsonElement lastMinuteOfDueDay = await planner.GetJsonAsync(DashboardPath(LiveVersion));

        Assert.Equal("2026-11-30", lastMinuteOfDueDay.GetProperty("dueSoonUntil").GetString());
        Assert.Equal(["SO-219"], OrderNumbers(lastMinuteOfDueDay.GetProperty("dueSoonOrders")));
        Assert.Empty(OrderNumbers(lastMinuteOfDueDay.GetProperty("overdueOrders")));

        // 23:00Z, still Monday in UTC but Tuesday in Berlin.
        clock.SetUtcNow(Berlin(24, 0));

        JsonElement nextDay = await planner.GetJsonAsync(DashboardPath(LiveVersion));

        Assert.Equal("2026-12-01", nextDay.GetProperty("dueSoonUntil").GetString());
        Assert.Empty(OrderNumbers(nextDay.GetProperty("dueSoonOrders")));
        Assert.Equal(["SO-219"], OrderNumbers(nextDay.GetProperty("overdueOrders")));
        Assert.True(nextDay.GetProperty("overdueOrders")[0].GetProperty("isLate").GetBoolean());
    }

    [Fact]
    public async Task AC_220_counts_the_queue_and_the_conflicts_and_lists_the_active_machines_that_are_down()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC220");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int brokenBuilderId = await CreateResourceAsync(admin, "AC220 Builder 2", plant.BuildingTypeId);
        int retiredPressId = await CreateResourceAsync(admin, "AC220 Press 3", plant.CuringTypeId);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int[] startedOrderLots = LotIds(await CreateOrderAsync(planner, NewOrder("SO-220-A", plant.ProductId, quantity: LotQuantity)));
        // Builder 1 is Running; the curing lot then waits on Press 1, which breaks down under it.
        await PlaceLotAsync(planner, startedOrderLots[0], plant.Builder1, Berlin(18, 8));
        await RecordProgressAsync(planner, startedOrderLots[0], Berlin(18, 8));
        await PlaceLotAsync(planner, startedOrderLots[1], plant.Press1, Berlin(18, 12));
        await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Breakdown", Berlin(18, 9), end: null));
        await CreateOrderAsync(planner, NewOrder("SO-220-B", plant.ProductId, quantity: LotQuantity));
        await CreateUnavailabilityAsync(planner, plant.Press2, Unavailability("Planned", Berlin(18, 9), Berlin(18, 11)));
        await CreateUnavailabilityAsync(planner, brokenBuilderId, Unavailability("Breakdown", Berlin(17, 9), Berlin(19, 9)));
        await CreateUnavailabilityAsync(planner, retiredPressId, Unavailability("Breakdown", Berlin(17, 9), end: null));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{retiredPressId}", new { name = "AC220 Press 3", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement dashboard = await viewer.GetJsonAsync(DashboardPath(LiveVersion));

        Assert.Equal(2, dashboard.GetProperty("unscheduledLotCount").GetInt32());
        Assert.Equal((await viewer.GetJsonAsync(UnscheduledLotsPath(LiveVersion))).GetArrayLength(), dashboard.GetProperty("unscheduledLotCount").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("conflictedPlacementCount").GetInt32());
        Assert.Equal((await viewer.GetJsonAsync(ConflictsPath(LiveVersion))).GetArrayLength(), dashboard.GetProperty("conflictedPlacementCount").GetInt32());
        JsonElement[] downMachines = [.. dashboard.GetProperty("downMachines").EnumerateArray()];
        Assert.Equal([brokenBuilderId, plant.Press1], downMachines.Select(machine => machine.GetProperty("id").GetInt32()));
        JsonElement press = downMachines[1];
        Assert.Equal(
            ("AC220 Press 1", "AC220 curing", "Plant default", true, "Down"),
            (press.GetProperty("name").GetString(),
                press.GetProperty("resourceType").GetProperty("name").GetString(),
                press.GetProperty("calendar").GetProperty("name").GetString(),
                press.GetProperty("isActive").GetBoolean(),
                press.GetProperty("status").GetString()));
    }

    private static (string? OrderNo, string? DueDate, string? Status, bool IsLate)[] OrderValues(JsonElement orders) =>
        [.. orders.EnumerateArray().Select(order => (
            order.GetProperty("orderNo").GetString(),
            order.GetProperty("dueDate").GetString(),
            order.GetProperty("status").GetString(),
            order.GetProperty("isLate").GetBoolean()))];

    private static string?[] OrderNumbers(JsonElement orders) => [.. orders.EnumerateArray().Select(order => order.GetProperty("orderNo").GetString())];
}

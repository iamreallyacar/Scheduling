using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Queries;

// F-SCH-2: the timeline read of a plant-local date range (DEC-45), on the plant default calendar
// in Europe/Berlin.
public sealed class TimelineTests : IAsyncDisposable
{
    private const string TwoWeeks = "from=2026-11-16&to=2026-11-29";

    private readonly FakeTimeProvider clock = new(Berlin(17, 11));
    private readonly SchedulerApiFixture api;

    public TimelineTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_185_every_role_reads_the_machines_by_type_with_status_working_periods_unavailabilities_and_placements_in_the_range()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC185");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int idlePress = await CreateResourceAsync(admin, "AC185 Press 3", plant.CuringTypeId);
        int extrusionTypeId = await CreateResourceTypeAsync(admin, "AC185 extrusion");
        int idleExtruder = await CreateResourceAsync(admin, "AC185 Extruder 1", extrusionTypeId);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement lateOrder = await CreateOrderAsync(planner, NewOrder("SO-185-A", plant.ProductId, dueDate: "2026-11-16"));
        JsonElement onTimeOrder = await CreateOrderAsync(planner, NewOrder("SO-185-B", plant.ProductId));
        JsonElement laterOrder = await CreateOrderAsync(planner, NewOrder("SO-185-C", plant.ProductId));
        await PlaceLotAsync(planner, LotIds(lateOrder)[0], plant.Builder1, Berlin(16, 8));
        JsonElement lateCuring = await PlaceLotAsync(planner, LotIds(lateOrder)[1], plant.Press1, Berlin(17, 6));
        JsonElement onTimeCuring = await PlaceLotAsync(planner, LotIds(onTimeOrder)[1], plant.Press2, Berlin(17, 6));
        await PlaceLotAsync(planner, LotIds(laterOrder)[1], plant.Press1, Berlin(30, 6));
        int breakdownId = await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Breakdown", Berlin(17, 11), end: null, reason: "Bladder leak"));
        await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Planned", Berlin(30, 0), Berlin(30, 4)));
        await CreateUnavailabilityAsync(planner, plant.Builder1, Unavailability("Planned", Berlin(17, 10), Berlin(17, 12)));
        await DeactivateAsync(admin, plant.Press2, "AC185 Press 2", plant.CuringTypeId);
        await DeactivateAsync(admin, idlePress, "AC185 Press 3", plant.CuringTypeId);
        await DeactivateAsync(admin, idleExtruder, "AC185 Extruder 1", extrusionTypeId);
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement timeline = await viewer.GetJsonAsync(TimelinePath(LiveVersion, TwoWeeks));

        Assert.Equal((Berlin(16, 0), Berlin(30, 0)), (timeline.GetProperty("start").GetDateTimeOffset(), timeline.GetProperty("end").GetDateTimeOffset()));
        Assert.Equal(
            [("AC185 building", ["AC185 Builder 1"]), ("AC185 curing", ["AC185 Press 1", "AC185 Press 2"])],
            timeline.GetProperty("resourceTypes").EnumerateArray().Select(type => (
                type.GetProperty("name").GetString(),
                type.GetProperty("resources").EnumerateArray().Select(resource => resource.GetProperty("name").GetString()).ToArray())));
        JsonElement builder = Resource(timeline, plant.Builder1);
        JsonElement press1 = Resource(timeline, plant.Press1);
        JsonElement press2 = Resource(timeline, plant.Press2);
        Assert.Equal(("Maintenance", true), (builder.GetProperty("status").GetString(), builder.GetProperty("isActive").GetBoolean()));
        Assert.Equal(("Down", "Idle", false), (press1.GetProperty("status").GetString(), press2.GetProperty("status").GetString(), press2.GetProperty("isActive").GetBoolean()));
        Assert.Equal([(Berlin(16, 0), Berlin(21, 0)), (Berlin(23, 0), Berlin(28, 0))], Periods(press1.GetProperty("workingPeriods")));

        JsonElement breakdown = Assert.Single(press1.GetProperty("unavailabilities").EnumerateArray());
        Assert.Equal((breakdownId, "Breakdown", Berlin(17, 11), JsonValueKind.Null, "Bladder leak"), (
            breakdown.GetProperty("id").GetInt32(),
            breakdown.GetProperty("kind").GetString(),
            breakdown.GetProperty("start").GetDateTimeOffset(),
            breakdown.GetProperty("end").ValueKind,
            breakdown.GetProperty("reason").GetString()));

        JsonElement placement = Assert.Single(press1.GetProperty("placements").EnumerateArray());
        Assert.Equal((lateCuring.GetProperty("lotId").GetInt32(), Berlin(17, 6), Berlin(17, 6).AddMinutes(CuringLotMinutes)), (
            placement.GetProperty("lotId").GetInt32(),
            placement.GetProperty("plannedStart").GetDateTimeOffset(),
            placement.GetProperty("plannedEnd").GetDateTimeOffset()));
        Assert.True(placement.GetProperty("placementId").GetInt32() > 0);
        Assert.Equal((1, LotQuantity, CuringLotMinutes, "Scheduled"), (
            placement.GetProperty("lotNo").GetInt32(),
            placement.GetProperty("quantity").GetInt32(),
            placement.GetProperty("durationMinutes").GetInt32(),
            placement.GetProperty("status").GetString()));
        Assert.Equal((lateOrder.GetProperty("id").GetInt32(), "SO-185-A", plant.ProductId, "AC185", true), (
            placement.GetProperty("orderId").GetInt32(),
            placement.GetProperty("orderNo").GetString(),
            placement.GetProperty("product").GetProperty("id").GetInt32(),
            placement.GetProperty("product").GetProperty("code").GetString(),
            placement.GetProperty("orderIsLate").GetBoolean()));
        Assert.Equal((2, plant.CuringTypeId, "AC185 curing"), (
            placement.GetProperty("sequenceNo").GetInt32(),
            placement.GetProperty("resourceType").GetProperty("id").GetInt32(),
            placement.GetProperty("resourceType").GetProperty("name").GetString()));
        JsonElement inactivePlacement = Assert.Single(press2.GetProperty("placements").EnumerateArray());
        Assert.Equal((onTimeCuring.GetProperty("lotId").GetInt32(), false), (inactivePlacement.GetProperty("lotId").GetInt32(), inactivePlacement.GetProperty("orderIsLate").GetBoolean()));
    }

    [Fact]
    public async Task AC_185_placements_and_unavailabilities_that_cross_the_range_edges_are_listed_with_their_stored_times_in_start_order()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC185E");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int firstLot = LotIds(await CreateOrderAsync(planner, NewOrder("SO-185-E1", plant.ProductId)))[1];
        int secondLot = LotIds(await CreateOrderAsync(planner, NewOrder("SO-185-E2", plant.ProductId)))[1];
        // From Fri 22:00 the curing lot pauses over the weekend and ends Mon 02:00.
        await PlaceLotAsync(planner, secondLot, plant.Press1, Berlin(23, 6));
        await PlaceLotAsync(planner, firstLot, plant.Press1, Berlin(20, 22));
        await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Planned", Berlin(22, 20), Berlin(23, 1)));

        JsonElement press1 = Resource(await planner.GetJsonAsync(TimelinePath(LiveVersion, "from=2026-11-23&to=2026-11-23")), plant.Press1);

        Assert.Equal(
            [(firstLot, Berlin(20, 22), Berlin(23, 2)), (secondLot, Berlin(23, 6), Berlin(23, 6).AddMinutes(CuringLotMinutes))],
            press1.GetProperty("placements").EnumerateArray().Select(placement => (
                placement.GetProperty("lotId").GetInt32(),
                placement.GetProperty("plannedStart").GetDateTimeOffset(),
                placement.GetProperty("plannedEnd").GetDateTimeOffset())));
        Assert.Equal(Berlin(22, 20), Assert.Single(press1.GetProperty("unavailabilities").EnumerateArray()).GetProperty("start").GetDateTimeOffset());
        Assert.Equal([(Berlin(23, 0), Berlin(24, 0))], Periods(press1.GetProperty("workingPeriods")));
    }

    // Summer time ends on Sun 2026-10-25, so Friday and the next week have different UTC offsets.
    [Fact]
    public async Task AC_186_working_periods_merge_the_shifts_and_leave_out_the_weekend_and_holidays_across_a_dst_change()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC186");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        await CreateHolidayAsync(admin, "2026-10-28", "AC186 holiday");

        JsonElement timeline = await admin.GetJsonAsync(TimelinePath(LiveVersion, "from=2026-10-23&to=2026-10-30"));

        Assert.Equal((Berlin(23, 0, month: 10), Berlin(31, 0, month: 10)), (timeline.GetProperty("start").GetDateTimeOffset(), timeline.GetProperty("end").GetDateTimeOffset()));
        Assert.Equal(
            [(Berlin(23, 0, month: 10), Berlin(24, 0, month: 10)), (Berlin(26, 0, month: 10), Berlin(28, 0, month: 10)), (Berlin(29, 0, month: 10), Berlin(31, 0, month: 10))],
            Periods(Resource(timeline, plant.Press1).GetProperty("workingPeriods")));
    }

    [Theory]
    [InlineData("to=2026-11-29", "from")]
    [InlineData("from=2026-11-16", "to")]
    [InlineData("from=2026-11-16&to=2026-11-15", "to")]
    [InlineData("from=2026-11-01&to=2026-12-02", "to")]
    [InlineData("from=0001-01-01&to=0001-01-02", "from")]
    [InlineData("from=9999-12-30&to=9999-12-31", "to")]
    public async Task AC_187_a_range_that_is_missing_reversed_longer_than_31_days_or_outside_the_supported_years_is_rejected(string query, string field)
    {
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement problem = await ProblemAssert.HasProblemAsync(await viewer.GetAsync(TimelinePath(LiveVersion, query)), HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);

        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.GetRawText());
    }

    [Fact]
    public async Task AC_187_a_range_of_31_days_is_read()
    {
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(TimelinePath(LiveVersion, "from=2026-11-01&to=2026-12-01"))).StatusCode);
    }

    [Fact]
    public async Task AC_185_AC_23_a_version_that_does_not_exist_is_not_found()
    {
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        await ProblemAssert.HasProblemAsync(await viewer.GetAsync(TimelinePath("999", TwoWeeks)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    private static async Task DeactivateAsync(ApiSession admin, int resourceId, string name, int resourceTypeId) =>
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{resourceId}", new { name, resourceTypeId, isActive = false })).StatusCode);

    private static JsonElement Resource(JsonElement timeline, int resourceId) =>
        timeline.GetProperty("resourceTypes").EnumerateArray()
            .SelectMany(type => type.GetProperty("resources").EnumerateArray())
            .Single(resource => resource.GetProperty("id").GetInt32() == resourceId);

    private static (DateTimeOffset Start, DateTimeOffset End)[] Periods(JsonElement periods) =>
        [.. periods.EnumerateArray().Select(period => (period.GetProperty("start").GetDateTimeOffset(), period.GetProperty("end").GetDateTimeOffset()))];
}

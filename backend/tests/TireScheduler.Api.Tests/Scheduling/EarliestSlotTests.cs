using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.Fakes;
using TireScheduler.Api.Tests.TestData;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Scheduling;

// F-SCH-11 at API level (acceptance-criteria.md section 5): the endpoint maps the engine's
// earliest-slot search, which the engine tests cover in detail. The clock is fixed on Mon
// 2026-11-16 09:30 plant time, so a search without notBefore starts there (AC-125).
public sealed class EarliestSlotTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = Berlin(16, 9, 30);

    private readonly FakeLotActivity lotActivity = new();
    private readonly SchedulerApiFixture api;

    public EarliestSlotTests(PostgresFixture postgres) =>
        api = SchedulerApiFixture.ForEachTest(postgres, new FakeTimeProvider(Now), lotActivity.ReplaceProgressIn);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_194_AC_125_AC_126_any_role_gets_the_earliest_slot_from_now_and_nothing_is_saved()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC194");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-194", plant.ProductId, quantity: LotQuantity));
        int curingLotId = LotIds(order)[1];
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement fromNow = await viewer.GetJsonAsync(EarliestSlotPath(LiveVersion, curingLotId, resourceId: null, notBefore: null));
        // notBefore is not checked for whole minutes: the search starts at the next one (A45).
        JsonElement onPress2 = await viewer.GetJsonAsync(EarliestSlotPath(LiveVersion, curingLotId, plant.Press2, Berlin(17, 8).AddSeconds(-30)));

        // Both presses are free from now; the tie goes to the name (AC-118).
        Assert.Equal(
            (curingLotId, plant.Press1, "AC194 Press 1", Now, Now.AddMinutes(CuringLotMinutes)),
            (fromNow.GetProperty("lotId").GetInt32(), fromNow.GetProperty("resource").GetProperty("id").GetInt32(),
                fromNow.GetProperty("resource").GetProperty("name").GetString(), fromNow.GetProperty("plannedStart").GetDateTimeOffset(),
                fromNow.GetProperty("plannedEnd").GetDateTimeOffset()));
        Assert.Equal(TimeSpan.Zero, fromNow.GetProperty("plannedStart").GetDateTimeOffset().Offset);
        Assert.Equal(
            (plant.Press2, Berlin(17, 8), Berlin(17, 8).AddMinutes(CuringLotMinutes)),
            (onPress2.GetProperty("resource").GetProperty("id").GetInt32(), onPress2.GetProperty("plannedStart").GetDateTimeOffset(), onPress2.GetProperty("plannedEnd").GetDateTimeOffset()));
        Assert.Equal(LotIds(order), QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
    }

    [Fact]
    public async Task AC_194_AC_124_a_started_lot_and_a_lot_of_a_cancelled_order_are_rejected_under_R8()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC124");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement started = await CreateOrderAsync(planner, NewOrder("SO-124-S", plant.ProductId, quantity: LotQuantity));
        lotActivity.StartedLotIds.Add(LotIds(started)[0]);
        JsonElement cancelled = await CreateOrderAsync(planner, NewOrder("SO-124-C", plant.ProductId, quantity: LotQuantity));
        await CancelOrderAsync(planner, cancelled.GetProperty("id").GetInt32());

        JsonElement startedProblem = await ProblemAssert.HasProblemAsync(
            await planner.GetAsync(EarliestSlotPath(LiveVersion, LotIds(started)[0], resourceId: null, notBefore: null)), HttpStatusCode.UnprocessableEntity, "rule.R8.started");
        JsonElement cancelledProblem = await ProblemAssert.HasProblemAsync(
            await planner.GetAsync(EarliestSlotPath(LiveVersion, LotIds(cancelled)[1], resourceId: null, notBefore: null)), HttpStatusCode.UnprocessableEntity, "rule.R8.cancelled");

        Assert.Equal(("R8", "lot", LotIds(started)[0]), (startedProblem.GetProperty("rule").GetString(), startedProblem.GetProperty("conflict").GetProperty("type").GetString(), startedProblem.GetProperty("conflict").GetProperty("id").GetInt32()));
        Assert.Equal(["rule.R8.cancelled"], cancelledProblem.GetProperty("violations").EnumerateArray().Select(violation => violation.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AC_122_no_slot_when_one_press_is_inactive_and_the_other_broken_down_without_an_end()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC122");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press2}", new { name = "AC122 Press 2", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Breakdown", Now, end: null));
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-122", plant.ProductId, quantity: LotQuantity));

        HttpResponseMessage response = await planner.GetAsync(EarliestSlotPath(LiveVersion, LotIds(order)[1], resourceId: null, notBefore: null));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, ProblemCodes.SlotNone);
    }

    [Fact]
    public async Task AC_195_an_unknown_machine_is_a_field_error_an_unfit_machine_has_no_slot_and_unknown_items_are_not_found()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC195");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press2}", new { name = "AC195 Press 2", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-195", plant.ProductId, quantity: LotQuantity));
        JsonElement deleted = await CreateOrderAsync(planner, NewOrder("SO-195-X", plant.ProductId, quantity: LotQuantity));
        Assert.Equal(HttpStatusCode.NoContent, (await planner.DeleteAsync(OrderPath(deleted.GetProperty("id").GetInt32()))).StatusCode);
        int curingLotId = LotIds(order)[1];

        JsonElement machineProblem = await ProblemAssert.HasProblemAsync(
            await planner.GetAsync(EarliestSlotPath(LiveVersion, curingLotId, 999_999, notBefore: null)), HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        await ProblemAssert.HasProblemAsync(await planner.GetAsync(EarliestSlotPath(LiveVersion, curingLotId, plant.Press2, notBefore: null)), HttpStatusCode.UnprocessableEntity, ProblemCodes.SlotNone);
        await ProblemAssert.HasProblemAsync(await planner.GetAsync(EarliestSlotPath(LiveVersion, curingLotId, plant.Builder1, notBefore: null)), HttpStatusCode.UnprocessableEntity, ProblemCodes.SlotNone);
        await ProblemAssert.HasProblemAsync(await planner.GetAsync(EarliestSlotPath(LiveVersion, 999_999, resourceId: null, notBefore: null)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(await planner.GetAsync(EarliestSlotPath(LiveVersion, LotIds(deleted)[0], resourceId: null, notBefore: null)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(await planner.GetAsync(EarliestSlotPath("999", curingLotId, resourceId: null, notBefore: null)), HttpStatusCode.NotFound, ProblemCodes.NotFound);

        Assert.True(machineProblem.GetProperty("errors").TryGetProperty("resourceId", out _));
    }
}

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

// The actions the lot and order answers allow (DEC-70), on the plant default calendar in
// Europe/Berlin. Where an action is not allowed the server must also reject it (R7).
public sealed class ActionTests : IAsyncDisposable
{
    private readonly FakeTimeProvider clock = new(Berlin(20, 12));
    private readonly SchedulerApiFixture api;

    public ActionTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_234_AC_213_AC_210_a_lot_allows_a_change_of_placement_until_it_starts_and_progress_while_it_is_placed_in_an_open_order()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC234");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-234-A", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        (int buildingLot, int curingLot) = (LotIds(order)[0], LotIds(order)[1]);
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement unplaced = await viewer.GetJsonAsync(OrderPath(orderId));
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        JsonElement placed = await viewer.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8));
        JsonElement started = await viewer.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8), Berlin(16, 10));
        JsonElement completed = await viewer.GetJsonAsync(OrderPath(orderId));

        Assert.Equal((true, false), ActionsOf(Lot(unplaced, buildingLot)));
        Assert.Equal((true, true), ActionsOf(Lot(placed, buildingLot)));
        Assert.Equal((false, true), ActionsOf(Lot(started, buildingLot)));
        Assert.Equal((false, true), ActionsOf(Lot(completed, buildingLot)));
        Assert.Equal((true, false), ActionsOf(Lot(completed, curingLot)));
        HttpResponseMessage moveStarted = await planner.PutAsync(PlacementPath(LiveVersion, buildingLot), Placement(plant.Builder1, Berlin(17, 8)));
        HttpResponseMessage startUnplaced = await planner.PutAsync(ProgressPath(curingLot), ProgressBody(Berlin(16, 10)));
        await ProblemAssert.HasProblemAsync(moveStarted, HttpStatusCode.UnprocessableEntity, "rule.R8.started");
        await ProblemAssert.HasProblemAsync(startUnplaced, HttpStatusCode.Conflict, "progress.not_placed");
    }

    [Fact]
    public async Task AC_234_AC_235_a_cancelled_order_allows_no_action_even_for_a_lot_that_ran()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC235");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement openOrder = await CreateOrderAsync(planner, NewOrder("SO-235-A", plant.ProductId, quantity: LotQuantity));
        JsonElement cancelledOrder = await CreateOrderAsync(planner, NewOrder("SO-235-B", plant.ProductId, quantity: LotQuantity));
        int cancelledOrderId = cancelledOrder.GetProperty("id").GetInt32();
        (int ranLot, int cancelledLot) = (LotIds(cancelledOrder)[0], LotIds(cancelledOrder)[1]);
        await PlaceLotAsync(planner, ranLot, plant.Builder1, Berlin(16, 8));
        await RecordProgressAsync(planner, ranLot, Berlin(16, 8), Berlin(16, 10));
        await CancelOrderAsync(planner, cancelledOrderId);
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement open = await viewer.GetJsonAsync(OrderPath(openOrder.GetProperty("id").GetInt32()));
        JsonElement cancelled = await viewer.GetJsonAsync(OrderPath(cancelledOrderId));

        Assert.True(open.GetProperty("actions").GetProperty("canScheduleAsap").GetBoolean());
        Assert.False(cancelled.GetProperty("actions").GetProperty("canScheduleAsap").GetBoolean());
        Assert.Equal((false, false), ActionsOf(Lot(cancelled, ranLot)));
        Assert.Equal((false, false), ActionsOf(Lot(cancelled, cancelledLot)));
        HttpResponseMessage placeCancelled = await planner.PutAsync(PlacementPath(LiveVersion, cancelledLot), Placement(plant.Press1, Berlin(17, 8)));
        HttpResponseMessage proposal = await planner.PostAsync(AsapProposalPath(LiveVersion, cancelledOrderId, notBefore: null));
        await ProblemAssert.HasProblemAsync(placeCancelled, HttpStatusCode.UnprocessableEntity, "rule.R8.cancelled");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, proposal.StatusCode);
    }

    [Fact]
    public async Task AC_236_the_timeline_and_the_conflict_list_carry_the_actions_of_each_placed_lot()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC236");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement runningOrder = await CreateOrderAsync(planner, NewOrder("SO-236-A", plant.ProductId, quantity: LotQuantity));
        JsonElement waitingOrder = await CreateOrderAsync(planner, NewOrder("SO-236-B", plant.ProductId, quantity: LotQuantity));
        (int runningLot, int waitingLot) = (LotIds(runningOrder)[1], LotIds(waitingOrder)[1]);
        await PlaceLotAsync(planner, runningLot, plant.Press1, Berlin(18, 8));
        await PlaceLotAsync(planner, waitingLot, plant.Press1, Berlin(19, 8));
        await RecordProgressAsync(planner, runningLot, Berlin(18, 8));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement timeline = await viewer.GetJsonAsync(TimelinePath(LiveVersion, "from=2026-11-16&to=2026-11-22"));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press1}", new { name = "AC236 Press 1", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);
        JsonElement conflicts = await viewer.GetJsonAsync(ConflictsPath(LiveVersion));

        JsonElement[] onTimeline = [.. timeline.GetProperty("resourceTypes").EnumerateArray()
            .SelectMany(type => type.GetProperty("resources").EnumerateArray())
            .SelectMany(resource => resource.GetProperty("placements").EnumerateArray())];
        Assert.Equal(
            [(runningLot, false, true), (waitingLot, true, true)],
            onTimeline.OrderBy(placement => placement.GetProperty("plannedStart").GetDateTimeOffset()).Select(placement => (placement.GetProperty("lotId").GetInt32(), ActionsOf(placement).CanChangePlacement, ActionsOf(placement).CanRecordProgress)));
        Assert.Equal(
            [(runningLot, false, true), (waitingLot, true, true)],
            conflicts.EnumerateArray().OrderBy(placement => placement.GetProperty("plannedStart").GetDateTimeOffset()).Select(placement => (placement.GetProperty("lotId").GetInt32(), ActionsOf(placement).CanChangePlacement, ActionsOf(placement).CanRecordProgress)));
    }

    private static (bool CanChangePlacement, bool CanRecordProgress) ActionsOf(JsonElement lotOrPlacement)
    {
        JsonElement actions = lotOrPlacement.GetProperty("actions");
        return (actions.GetProperty("canChangePlacement").GetBoolean(), actions.GetProperty("canRecordProgress").GetBoolean());
    }
}

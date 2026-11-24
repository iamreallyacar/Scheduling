using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.Fakes;
using TireScheduler.Api.Tests.TestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Scheduling;

// F-SCH-16 at API level (acceptance-criteria.md section 5): the proposal maps the engine's
// ScheduleOrderAsap, which the engine tests cover in detail, and the commit makes it again under
// its locks (DEC-60). The clock is fixed on Mon 2026-11-16 09:30 plant time, and the plant
// default calendar works around the clock from Monday to Friday, so lots follow each other.
public sealed class OrderProposalTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = Berlin(16, 9, 30);

    private readonly FakeLotActivity lotActivity = new();
    private readonly SchedulerApiFixture api;

    public OrderProposalTests(PostgresFixture postgres) =>
        api = SchedulerApiFixture.ForEachTest(postgres, new FakeTimeProvider(Now), lotActivity.ReplaceProgressIn);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_199_AC_128_a_planner_gets_every_lot_proposed_from_now_and_nothing_is_saved()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC199");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-199", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        int[] lotIds = LotIds(order);
        DateTimeOffset buildingEnd = Now.AddMinutes(BuildingLotMinutes);

        JsonElement proposal = await ReadProposalAsync(planner, orderId, notBefore: null);

        Assert.Equal((orderId, Now), (proposal.GetProperty("orderId").GetInt32(), proposal.GetProperty("notBefore").GetDateTimeOffset()));
        // Both presses are free after the building lot; the tie goes to the name (AC-118).
        Assert.Equal(
            [
                new ProposalLot(lotIds[0], 1, 1, plant.BuildingTypeId, plant.Builder1, "AC199 Builder 1", Now, buildingEnd, IsProposed: true),
                new ProposalLot(lotIds[1], 1, 2, plant.CuringTypeId, plant.Press1, "AC199 Press 1", buildingEnd, buildingEnd.AddMinutes(CuringLotMinutes), IsProposed: true),
            ],
            LotsOf(proposal));
        Assert.Equal(TimeSpan.Zero, proposal.GetProperty("lots")[0].GetProperty("plannedStart").GetDateTimeOffset().Offset);
        Assert.Equal((buildingEnd.AddMinutes(CuringLotMinutes), false), (proposal.GetProperty("finishesAt").GetDateTimeOffset(), proposal.GetProperty("wouldBeLate").GetBoolean()));
        Assert.Equal(lotIds, QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));

        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);
        await ProblemAssert.HasProblemAsync(await viewer.PostAsync(AsapProposalPath(LiveVersion, orderId, notBefore: null)), HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        await ProblemAssert.HasProblemAsync(await planner.PostAsync(AsapProposalPath(LiveVersion, 999_999, notBefore: null)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(await planner.PostAsync(AsapProposalPath("999", orderId, notBefore: null)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_199_a_proposal_that_ends_after_the_due_day_would_make_the_order_late()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC199L");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-199-L", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-16"));
        DateTimeOffset tuesdayMorning = Berlin(17, 8);

        JsonElement proposal = await ReadProposalAsync(planner, order.GetProperty("id").GetInt32(), tuesdayMorning);

        Assert.Equal(
            (tuesdayMorning, tuesdayMorning, tuesdayMorning.AddMinutes(BuildingLotMinutes + CuringLotMinutes), true),
            (proposal.GetProperty("notBefore").GetDateTimeOffset(), proposal.GetProperty("lots")[0].GetProperty("plannedStart").GetDateTimeOffset(),
                proposal.GetProperty("finishesAt").GetDateTimeOffset(), proposal.GetProperty("wouldBeLate").GetBoolean()));
        // The due day is not over yet and nothing is placed, so only the proposal is late.
        Assert.False(order.GetProperty("isLate").GetBoolean());
    }

    [Fact]
    public async Task AC_130_AC_132_placed_lots_stay_and_an_order_with_every_lot_placed_proposes_and_saves_nothing()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC130");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-130", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        int[] lotIds = LotIds(order);
        DateTimeOffset curingStart = Berlin(17, 8);
        await PlaceLotAsync(planner, lotIds[1], plant.Press2, curingStart);

        JsonElement partly = await ReadProposalAsync(planner, orderId, notBefore: null);
        await PlaceLotAsync(planner, lotIds[0], plant.Builder1, Berlin(16, 12));
        JsonElement placed = await ReadProposalAsync(planner, orderId, notBefore: null);
        HttpResponseMessage commit = await planner.PostAsync(AsapCommitPath(LiveVersion, orderId), CommitBodyOf(placed));

        Assert.Equal(
            [
                new ProposalLot(lotIds[0], 1, 1, plant.BuildingTypeId, plant.Builder1, "AC130 Builder 1", Now, Now.AddMinutes(BuildingLotMinutes), IsProposed: true),
                new ProposalLot(lotIds[1], 1, 2, plant.CuringTypeId, plant.Press2, "AC130 Press 2", curingStart, curingStart.AddMinutes(CuringLotMinutes), IsProposed: false),
            ],
            LotsOf(partly));
        Assert.All(LotsOf(placed), lot => Assert.False(lot.IsProposed));
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        Assert.Empty((await ApiSession.ReadJsonAsync(commit)).EnumerateArray());
        JsonElement orderAfter = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal(Berlin(16, 12), Lot(orderAfter, lotIds[0]).GetProperty("placement").GetProperty("plannedStart").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC_131_a_lot_without_a_slot_is_named_and_nothing_is_proposed()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC131");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Breakdown", Now, end: null));
        await CreateUnavailabilityAsync(planner, plant.Press2, Unavailability("Breakdown", Now, end: null));
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-131", plant.ProductId, quantity: LotQuantity));

        JsonElement problem = await ProblemAssert.HasProblemAsync(
            await planner.PostAsync(AsapProposalPath(LiveVersion, order.GetProperty("id").GetInt32(), notBefore: null)), HttpStatusCode.UnprocessableEntity, ProblemCodes.SlotNone);

        Assert.Equal(LotIds(order)[1], problem.GetProperty("lotId").GetInt32());
        Assert.Contains("Lot 1 of stage 2 (AC131 curing)", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC_133_a_cancelled_order_is_rejected_under_R8_at_proposal_and_at_commit()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC133");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-133", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        JsonElement proposal = await ReadProposalAsync(planner, orderId, notBefore: null);
        await CancelOrderAsync(planner, orderId);

        await ProblemAssert.HasProblemAsync(await planner.PostAsync(AsapProposalPath(LiveVersion, orderId, notBefore: null)), HttpStatusCode.UnprocessableEntity, "rule.R8.cancelled");
        await ProblemAssert.HasProblemAsync(await planner.PostAsync(AsapCommitPath(LiveVersion, orderId), CommitBodyOf(proposal)), HttpStatusCode.UnprocessableEntity, "rule.R8.cancelled");
    }

    [Fact]
    public async Task AC_134_AC_200_an_unchanged_proposal_is_saved_whole()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC134");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-134", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        JsonElement proposal = await ReadProposalAsync(planner, orderId, notBefore: null);

        HttpResponseMessage response = await planner.PostAsync(AsapCommitPath(LiveVersion, orderId), CommitBodyOf(proposal));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        (int LotId, int ResourceId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd)[] proposed =
            [.. LotsOf(proposal).Select(lot => (lot.LotId, lot.ResourceId, lot.PlannedStart, lot.PlannedEnd))];
        Assert.Equal(proposed, (await ApiSession.ReadJsonAsync(response)).EnumerateArray().Select(placement =>
            (placement.GetProperty("lotId").GetInt32(), PlacementValues(placement).ResourceId, PlacementValues(placement).PlannedStart, PlacementValues(placement).PlannedEnd)));
        JsonElement orderAfter = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal(proposed, LotIds(orderAfter).Select(lotId =>
        {
            JsonElement placement = Lot(orderAfter, lotId).GetProperty("placement");
            return (lotId, placement.GetProperty("resource").GetProperty("id").GetInt32(), placement.GetProperty("plannedStart").GetDateTimeOffset(), placement.GetProperty("plannedEnd").GetDateTimeOffset());
        }));
        Assert.Empty(QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
    }

    // The search may pick any machine of a lot's type, so the commit locks all of them (DEC-60). Press 2
    // is held because the proposal picks Press 1: a commit that locked only the machines it uses
    // would not wait.
    [Fact]
    public async Task AC_134_a_commit_waits_while_another_command_holds_a_machine_of_a_lot_type()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC134L");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int orderId = (await CreateOrderAsync(planner, NewOrder("SO-134-L", plant.ProductId, quantity: LotQuantity))).GetProperty("id").GetInt32();
        JsonElement proposal = await ReadProposalAsync(planner, orderId, notBefore: null);
        Assert.DoesNotContain(plant.Press2, LotsOf(proposal).Select(lot => lot.ResourceId));

        await using HeldRowLock heldPress = await HeldRowLock.TakeAsync<MasterDataDbContext, Resource>(api.Factory, plant.Press2);
        Task<HttpResponseMessage> commit = planner.PostAsync(AsapCommitPath(LiveVersion, orderId), CommitBodyOf(proposal));
        await heldPress.ReleaseOnceItWaitsAsync(commit);

        Assert.Equal(HttpStatusCode.OK, (await commit).StatusCode);
    }

    [Fact]
    public async Task AC_135_a_changed_start_or_a_taken_slot_makes_the_proposal_stale_and_nothing_is_saved()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC135");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-135", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        JsonElement other = await CreateOrderAsync(planner, NewOrder("SO-135-B", plant.ProductId, quantity: LotQuantity));
        JsonElement proposal = await ReadProposalAsync(planner, orderId, notBefore: null);
        ProposalLot[] lots = LotsOf(proposal);
        object changedStart = new
        {
            notBefore = Now,
            placements = lots.Select((lot, index) => new { lotId = lot.LotId, resourceId = lot.ResourceId, plannedStart = index == 0 ? lot.PlannedStart.AddMinutes(15) : lot.PlannedStart }).ToArray(),
        };

        HttpResponseMessage changedResponse = await planner.PostAsync(AsapCommitPath(LiveVersion, orderId), changedStart);
        // The other order's curing lot takes the press and start proposed for this order's.
        await PlaceLotAsync(planner, LotIds(other)[1], lots[1].ResourceId, lots[1].PlannedStart);
        HttpResponseMessage takenResponse = await planner.PostAsync(AsapCommitPath(LiveVersion, orderId), CommitBodyOf(proposal));

        await ProblemAssert.HasProblemAsync(changedResponse, HttpStatusCode.Conflict, ProblemCodes.ProposalStale);
        await ProblemAssert.HasProblemAsync(takenResponse, HttpStatusCode.Conflict, ProblemCodes.ProposalStale);
        int[] stillQueued = [.. LotIds(order), LotIds(other)[0]];
        Assert.Equal(stillQueued, QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
    }

    [Fact]
    public async Task AC_136_AC_200_a_lot_of_another_order_a_lot_twice_and_missing_or_unfit_fields_are_field_errors()
    {
        TestPlant plant = await SchedulingTestData.CreatePlantAsync(api.Factory, "AC136");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-136", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        JsonElement other = await CreateOrderAsync(planner, NewOrder("SO-136-B", plant.ProductId, quantity: LotQuantity));
        ProposalLot[] lots = LotsOf(await ReadProposalAsync(planner, orderId, notBefore: null));
        object Body(params object[] placements) => new { notBefore = Now, placements };
        object Sent(ProposalLot lot, int? lotId = null) => new { lotId = lotId ?? lot.LotId, resourceId = lot.ResourceId, plannedStart = lot.PlannedStart };

        JsonElement otherOrderLot = await CommitProblemAsync(planner, orderId, Body(Sent(lots[0]), Sent(lots[1], LotIds(other)[1])));
        JsonElement lotTwice = await CommitProblemAsync(planner, orderId, Body(Sent(lots[0]), Sent(lots[0])));
        JsonElement missing = await CommitProblemAsync(planner, orderId, new { placements = new object[] { new { lotId = lots[0].LotId } } });
        JsonElement noPlacements = await CommitProblemAsync(planner, orderId, new { notBefore = Now });
        JsonElement notWholeMinute = await CommitProblemAsync(planner, orderId, Body(new { lotId = lots[0].LotId, resourceId = lots[0].ResourceId, plannedStart = lots[0].PlannedStart.AddSeconds(30) }, Sent(lots[1])));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);
        HttpResponseMessage viewerResponse = await viewer.PostAsync(AsapCommitPath(LiveVersion, orderId), Body(Sent(lots[0]), Sent(lots[1])));

        Assert.Equal(["placements[1].lotId"], ProblemAssert.ErrorFields(otherOrderLot));
        Assert.Equal(["placements[1].lotId"], ProblemAssert.ErrorFields(lotTwice));
        Assert.Equal(["notBefore", "placements[0].plannedStart", "placements[0].resourceId"], ProblemAssert.ErrorFields(missing));
        Assert.Equal(["placements"], ProblemAssert.ErrorFields(noPlacements));
        Assert.Equal(["placements[0].plannedStart"], ProblemAssert.ErrorFields(notWholeMinute));
        await ProblemAssert.HasProblemAsync(viewerResponse, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        int[] everyLot = [.. LotIds(order), .. LotIds(other)];
        Assert.Equal(everyLot, QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
    }

    private static async Task<JsonElement> ReadProposalAsync(ApiSession planner, int orderId, DateTimeOffset? notBefore)
    {
        HttpResponseMessage response = await planner.PostAsync(AsapProposalPath(LiveVersion, orderId, notBefore));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"The proposal answered {response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return await ApiSession.ReadJsonAsync(response);
    }

    private static async Task<JsonElement> CommitProblemAsync(ApiSession planner, int orderId, object body) =>
        await ProblemAssert.HasProblemAsync(await planner.PostAsync(AsapCommitPath(LiveVersion, orderId), body), HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);

    private static ProposalLot[] LotsOf(JsonElement proposal) =>
        [.. proposal.GetProperty("lots").EnumerateArray().Select(lot => new ProposalLot(
            lot.GetProperty("lotId").GetInt32(),
            lot.GetProperty("lotNo").GetInt32(),
            lot.GetProperty("sequenceNo").GetInt32(),
            lot.GetProperty("resourceType").GetProperty("id").GetInt32(),
            lot.GetProperty("resource").GetProperty("id").GetInt32(),
            lot.GetProperty("resource").GetProperty("name").GetString(),
            lot.GetProperty("plannedStart").GetDateTimeOffset(),
            lot.GetProperty("plannedEnd").GetDateTimeOffset(),
            lot.GetProperty("isProposed").GetBoolean()))];

    private sealed record ProposalLot(int LotId, int LotNo, int SequenceNo, int ResourceTypeId, int ResourceId, string? ResourceName, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd, bool IsProposed);
}

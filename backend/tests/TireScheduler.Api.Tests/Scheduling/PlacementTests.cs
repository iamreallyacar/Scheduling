using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.Fakes;
using TireScheduler.Api.Tests.TestData;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Scheduling;

// F-SCH-1 and the API level of the scheduling rules (acceptance-criteria.md sections 4 and 8), on
// the plant default calendar in Europe/Berlin. Lot progress comes from FakeLotActivity, so R8 is
// also tested in states the API cannot reach (DEC-62); Progress/LotProgressTests.cs tests it with
// real progress.
public sealed class PlacementTests : IAsyncDisposable
{
    private readonly PostgresFixture postgres;
    private readonly FakeLotActivity lotActivity = new();
    private readonly SchedulerApiFixture api;

    public PlacementTests(PostgresFixture postgres)
    {
        this.postgres = postgres;
        api = SchedulerApiFixture.ForEachTest(postgres, replaceServices: lotActivity.ReplaceProgressIn);
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_176_AC_58_AC_71_a_planner_places_and_moves_a_lot_and_the_reads_follow()
    {
        TestPlant plant = await CreatePlantAsync("AC176");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-176", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        int curingLotId = LotIds(order)[1];

        // The curing lot from Fri 22:00 pauses over the weekend and ends Mon 02:00 (R6).
        HttpResponseMessage placed = await planner.PutAsync(PlacementPath(LiveVersion, curingLotId), Placement(plant.Press1, Berlin(20, 22)));
        JsonElement placedBody = await ApiSession.ReadJsonAsync(placed);
        HttpResponseMessage moved = await planner.PutAsync(PlacementPath(LiveVersion, curingLotId), Placement(plant.Press2, Berlin(16, 8)));
        JsonElement movedBody = await ApiSession.ReadJsonAsync(moved);

        Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        Assert.Equal(
            (curingLotId, plant.Press1, Berlin(20, 22), Berlin(23, 2)),
            (placedBody.GetProperty("lotId").GetInt32(), placedBody.GetProperty("resourceId").GetInt32(), placedBody.GetProperty("plannedStart").GetDateTimeOffset(), placedBody.GetProperty("plannedEnd").GetDateTimeOffset()));
        Assert.Equal(TimeSpan.Zero, placedBody.GetProperty("plannedEnd").GetDateTimeOffset().Offset);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal((plant.Press2, Berlin(16, 8), Berlin(16, 8).AddMinutes(CuringLotMinutes)), PlacementValues(movedBody));
        // The old slot is free again: the move left no placement behind on Press 1.
        JsonElement other = await CreateOrderAsync(planner, NewOrder("SO-176-B", plant.ProductId, quantity: LotQuantity));
        await PlaceLotAsync(planner, LotIds(other)[1], plant.Press1, Berlin(20, 22));

        JsonElement detail = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal("Scheduled", detail.GetProperty("status").GetString());
        JsonElement curingLot = Lot(detail, curingLotId);
        Assert.Equal("Scheduled", curingLot.GetProperty("status").GetString());
        JsonElement livePlacement = curingLot.GetProperty("placement");
        Assert.Equal(
            (plant.Press2, "AC176 Press 2", Berlin(16, 8), Berlin(16, 8).AddMinutes(CuringLotMinutes)),
            (livePlacement.GetProperty("resource").GetProperty("id").GetInt32(),
                livePlacement.GetProperty("resource").GetProperty("name").GetString(),
                livePlacement.GetProperty("plannedStart").GetDateTimeOffset(),
                livePlacement.GetProperty("plannedEnd").GetDateTimeOffset()));
        Assert.Equal(JsonValueKind.Null, Lot(detail, LotIds(order)[0]).GetProperty("placement").ValueKind);
        Assert.Equal("Scheduled", ListedOrder(await planner.GetJsonAsync(OrdersPath), orderId).GetProperty("status").GetString());
        Assert.Equal([LotIds(order)[0], LotIds(other)[0]], QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))).Order());
    }

    [Fact]
    public async Task AC_57_a_live_planned_end_after_the_due_day_makes_the_order_late_in_the_detail_and_the_list()
    {
        TestPlant plant = await CreatePlantAsync("AC57");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-57", plant.ProductId, quantity: LotQuantity, dueDate: "2027-03-01"));
        int orderId = order.GetProperty("id").GetInt32();
        Assert.False(order.GetProperty("isLate").GetBoolean());

        await PlaceLotAsync(planner, LotIds(order)[0], plant.Builder1, Berlin(2, 8, month: 3, year: 2027));

        Assert.True((await planner.GetJsonAsync(OrderPath(orderId))).GetProperty("isLate").GetBoolean());
        Assert.True(ListedOrder(await planner.GetJsonAsync($"{OrdersPath}?late=true"), orderId).GetProperty("isLate").GetBoolean());
    }

    [Fact]
    public async Task AC_177_AC_23_missing_and_invalid_values_are_reported_by_field_and_unknown_items_are_not_found()
    {
        TestPlant plant = await CreatePlantAsync("AC177");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-177", plant.ProductId, quantity: LotQuantity));
        JsonElement deleted = await CreateOrderAsync(planner, NewOrder("SO-177-X", plant.ProductId, quantity: LotQuantity));
        Assert.Equal(HttpStatusCode.NoContent, (await planner.DeleteAsync(OrderPath(deleted.GetProperty("id").GetInt32()))).StatusCode);
        string path = PlacementPath(LiveVersion, LotIds(order)[0]);

        HttpResponseMessage empty = await planner.PutAsync(path, new { });
        HttpResponseMessage notWholeMinute = await planner.PutAsync(path, Placement(plant.Builder1, Berlin(16, 8).AddSeconds(30)));
        HttpResponseMessage unknownMachine = await planner.PutAsync(path, Placement(999_999, Berlin(16, 8)));
        HttpResponseMessage unknownLot = await planner.PutAsync(PlacementPath(LiveVersion, 999_999), Placement(plant.Builder1, Berlin(16, 8)));
        HttpResponseMessage lotOfDeletedOrder = await planner.PutAsync(PlacementPath(LiveVersion, LotIds(deleted)[0]), Placement(plant.Builder1, Berlin(16, 8)));
        HttpResponseMessage unknownVersion = await planner.PutAsync(PlacementPath("999", LotIds(order)[0]), Placement(plant.Builder1, Berlin(16, 8)));

        JsonElement emptyProblem = await ProblemAssert.HasProblemAsync(empty, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(emptyProblem.GetProperty("errors").TryGetProperty("resourceId", out _));
        Assert.True(emptyProblem.GetProperty("errors").TryGetProperty("plannedStart", out _));
        JsonElement minuteProblem = await ProblemAssert.HasProblemAsync(notWholeMinute, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(minuteProblem.GetProperty("errors").TryGetProperty("plannedStart", out _));
        JsonElement machineProblem = await ProblemAssert.HasProblemAsync(unknownMachine, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(machineProblem.GetProperty("errors").TryGetProperty("resourceId", out _));
        await ProblemAssert.HasProblemAsync(unknownLot, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(lotOfDeletedOrder, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(unknownVersion, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.Equal(LotIds(order), QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
    }

    [Fact]
    public async Task AC_178_a_removed_placement_puts_the_lot_back_in_the_queue_and_AC_11_only_a_planner_may_write()
    {
        TestPlant plant = await CreatePlantAsync("AC178");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-178", plant.ProductId, quantity: LotQuantity));
        int buildingLotId = LotIds(order)[0];
        await PlaceLotAsync(planner, buildingLotId, plant.Builder1, Berlin(16, 8));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage viewerRemoval = await viewer.DeleteAsync(PlacementPath(LiveVersion, buildingLotId));
        HttpResponseMessage removal = await planner.DeleteAsync(PlacementPath(LiveVersion, buildingLotId));
        HttpResponseMessage secondRemoval = await planner.DeleteAsync(PlacementPath(LiveVersion, buildingLotId));

        await ProblemAssert.HasProblemAsync(viewerRemoval, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        await ProblemAssert.HasProblemAsync(secondRemoval, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.Equal(LotIds(order), QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
        Assert.Equal("Pending", (await planner.GetJsonAsync(OrderPath(order.GetProperty("id").GetInt32()))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC_74_AC_75_a_machine_of_another_type_or_an_inactive_one_is_rejected_naming_the_machine()
    {
        TestPlant plant = await CreatePlantAsync("AC74");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press2}", new { name = "AC74 Press 2", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int curingLotId = LotIds(await CreateOrderAsync(planner, NewOrder("SO-74", plant.ProductId, quantity: LotQuantity)))[1];

        HttpResponseMessage onBuilder = await planner.PutAsync(PlacementPath(LiveVersion, curingLotId), Placement(plant.Builder1, Berlin(16, 8)));
        HttpResponseMessage onInactive = await planner.PutAsync(PlacementPath(LiveVersion, curingLotId), Placement(plant.Press2, Berlin(16, 8)));

        AssertRejection(await ProblemAssert.HasProblemAsync(onBuilder, HttpStatusCode.UnprocessableEntity, "rule.R1.resource_type"), "R1", "resource", plant.Builder1);
        AssertRejection(await ProblemAssert.HasProblemAsync(onInactive, HttpStatusCode.UnprocessableEntity, "rule.R1.inactive"), "R1", "resource", plant.Press2);
    }

    [Fact]
    public async Task AC_78_AC_111_AC_109_AC_179_an_overlap_is_rejected_with_every_violation_and_nothing_is_moved_or_saved()
    {
        TestPlant plant = await CreatePlantAsync("AC78");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int lotA = LotIds(await CreateOrderAsync(planner, NewOrder("SO-78-A", plant.ProductId, quantity: LotQuantity)))[1];
        JsonElement orderB = await CreateOrderAsync(planner, NewOrder("SO-78-B", plant.ProductId, quantity: LotQuantity));
        int lotB = LotIds(orderB)[1];
        JsonElement placementA = await PlaceLotAsync(planner, lotA, plant.Press1, Berlin(16, 8));

        HttpResponseMessage response = await planner.PutAsync(PlacementPath(LiveVersion, lotB), Placement(plant.Press1, Berlin(16, 11)));

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R2.overlap");
        int placementAId = await PlacementIdAsync(lotA);
        AssertRejection(problem, "R2", "placement", placementAId);
        JsonElement violation = Assert.Single(problem.GetProperty("violations").EnumerateArray());
        Assert.Equal(
            ("R2", "rule.R2.overlap", "placement", placementAId, JsonValueKind.Null),
            (violation.GetProperty("rule").GetString(),
                violation.GetProperty("code").GetString(),
                violation.GetProperty("conflict").GetProperty("type").GetString(),
                violation.GetProperty("conflict").GetProperty("id").GetInt32(),
                violation.GetProperty("earliestAllowedStart").ValueKind));
        Assert.Equal(problem.GetProperty("detail").GetString(), violation.GetProperty("detail").GetString());
        Assert.Equal(JsonValueKind.Null, Lot(await planner.GetJsonAsync(OrderPath(orderB.GetProperty("id").GetInt32())), lotB).GetProperty("placement").ValueKind);
        Assert.Equal((plant.Press1, Berlin(16, 8), Berlin(16, 8).AddMinutes(CuringLotMinutes)), PlacementValues(placementA));
    }

    [Fact]
    public async Task AC_84_a_lot_over_planned_maintenance_is_rejected_naming_the_maintenance()
    {
        TestPlant plant = await CreatePlantAsync("AC84");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int maintenanceId = await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Planned", Berlin(17, 6), Berlin(17, 10)));
        int curingLotId = LotIds(await CreateOrderAsync(planner, NewOrder("SO-84", plant.ProductId, quantity: LotQuantity)))[1];

        HttpResponseMessage response = await planner.PutAsync(PlacementPath(LiveVersion, curingLotId), Placement(plant.Press1, Berlin(17, 4)));

        AssertRejection(await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R3.unavailable"), "R3", "unavailability", maintenanceId);
    }

    [Fact]
    public async Task AC_89_AC_179_a_successor_before_its_predecessor_ends_is_rejected_with_the_earliest_allowed_start()
    {
        TestPlant plant = await CreatePlantAsync("AC89");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int[] lotIds = LotIds(await CreateOrderAsync(planner, NewOrder("SO-89", plant.ProductId, quantity: LotQuantity)));
        await PlaceLotAsync(planner, lotIds[0], plant.Builder1, Berlin(16, 8));

        HttpResponseMessage response = await planner.PutAsync(PlacementPath(LiveVersion, lotIds[1]), Placement(plant.Press1, Berlin(16, 9)));

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R4.dependency");
        AssertRejection(problem, "R4", "lot", lotIds[0]);
        JsonElement violation = Assert.Single(problem.GetProperty("violations").EnumerateArray());
        Assert.Equal(Berlin(16, 8).AddMinutes(BuildingLotMinutes), violation.GetProperty("earliestAllowedStart").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC_102_a_start_on_a_weekend_at_friday_midnight_or_on_a_holiday_is_rejected()
    {
        TestPlant plant = await CreatePlantAsync("AC102");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        await CreateHolidayAsync(admin, "2026-11-26", "AC-102 holiday");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int buildingLotId = LotIds(await CreateOrderAsync(planner, NewOrder("SO-102", plant.ProductId, quantity: LotQuantity)))[0];

        foreach (DateTimeOffset start in new[] { Berlin(21, 10), Berlin(21, 0), Berlin(26, 10) })
        {
            HttpResponseMessage response = await planner.PutAsync(PlacementPath(LiveVersion, buildingLotId), Placement(plant.Builder1, start));
            AssertRejection(await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R6.outside_working_time"), "R6", "resource", plant.Builder1);
        }
    }

    [Fact]
    public async Task AC_112_AC_113_a_started_lot_cannot_be_placed_moved_or_unplaced()
    {
        TestPlant plant = await CreatePlantAsync("AC112");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int[] completedOrderLots = LotIds(await CreateOrderAsync(planner, NewOrder("SO-112", plant.ProductId, quantity: LotQuantity)));
        int[] runningOrderLots = LotIds(await CreateOrderAsync(planner, NewOrder("SO-113", plant.ProductId, quantity: LotQuantity)));
        int completedPlaced = completedOrderLots[0];
        int completedUnplaced = completedOrderLots[1];
        int inProgress = runningOrderLots[0];
        await PlaceLotAsync(planner, completedPlaced, plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, inProgress, plant.Builder1, Berlin(16, 10));
        lotActivity.StartedLotIds.UnionWith([completedPlaced, completedUnplaced, inProgress]);
        lotActivity.CompletedLotIds.UnionWith([completedPlaced, completedUnplaced]);

        HttpResponseMessage[] responses =
        [
            await planner.PutAsync(PlacementPath(LiveVersion, completedUnplaced), Placement(plant.Press1, Berlin(17, 8))),
            await planner.PutAsync(PlacementPath(LiveVersion, completedPlaced), Placement(plant.Builder1, Berlin(17, 8))),
            await planner.DeleteAsync(PlacementPath(LiveVersion, completedPlaced)),
            await planner.PutAsync(PlacementPath(LiveVersion, inProgress), Placement(plant.Builder1, Berlin(17, 8))),
            await planner.DeleteAsync(PlacementPath(LiveVersion, inProgress)),
        ];

        int[] conflictLots = [completedUnplaced, completedPlaced, completedPlaced, inProgress, inProgress];
        foreach ((HttpResponseMessage response, int lotId) in responses.Zip(conflictLots))
        {
            AssertRejection(await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R8.started"), "R8", "lot", lotId);
        }

        Assert.Equal(Berlin(16, 8), (await StoredPlacementAsync(completedPlaced))?.PlannedStart);
        Assert.Equal(Berlin(16, 10), (await StoredPlacementAsync(inProgress))?.PlannedStart);
    }

    [Fact]
    public async Task AC_114_a_lot_of_a_cancelled_order_cannot_be_placed()
    {
        TestPlant plant = await CreatePlantAsync("AC114");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-114", plant.ProductId, quantity: LotQuantity));
        await CancelOrderAsync(planner, order.GetProperty("id").GetInt32());

        HttpResponseMessage response = await planner.PutAsync(PlacementPath(LiveVersion, LotIds(order)[0]), Placement(plant.Builder1, Berlin(16, 8)));

        AssertRejection(await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R8.cancelled"), "R8", "lot", LotIds(order)[0]);
    }

    [Fact]
    public async Task AC_82_placements_of_another_version_do_not_conflict_with_the_live_version()
    {
        TestPlant plant = await CreatePlantAsync("AC82");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int lotA = LotIds(await CreateOrderAsync(planner, NewOrder("SO-82-A", plant.ProductId, quantity: LotQuantity)))[1];
        int lotB = LotIds(await CreateOrderAsync(planner, NewOrder("SO-82-B", plant.ProductId, quantity: LotQuantity)))[1];
        // Scenarios have no API before F-SCH-12, so the scenario and its placement are written directly.
        int scenarioId = await AddScenarioPlacementAsync(api.Factory, lotA, plant.Press1, Berlin(16, 8), Berlin(16, 8).AddMinutes(CuringLotMinutes));

        HttpResponseMessage live = await planner.PutAsync(PlacementPath(LiveVersion, lotB), Placement(plant.Press1, Berlin(16, 8)));
        HttpResponseMessage inScenario = await planner.PutAsync(PlacementPath(scenarioId.ToString(CultureInfo.InvariantCulture), lotB), Placement(plant.Press1, Berlin(16, 8)));

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        await ProblemAssert.HasProblemAsync(inScenario, HttpStatusCode.UnprocessableEntity, "rule.R2.overlap");
        Assert.Contains(lotA, QueuedLotIds(await planner.GetJsonAsync(UnscheduledLotsPath(LiveVersion))));
    }

    [Fact]
    public async Task AC_83_of_two_overlapping_placements_at_the_same_moment_exactly_one_is_saved()
    {
        TestPlant plant = await CreatePlantAsync("AC83");
        using ApiSession firstPlanner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        using ApiSession secondPlanner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int lotA = LotIds(await CreateOrderAsync(firstPlanner, NewOrder("SO-83-A", plant.ProductId, quantity: LotQuantity)))[1];
        int lotB = LotIds(await CreateOrderAsync(firstPlanner, NewOrder("SO-83-B", plant.ProductId, quantity: LotQuantity)))[1];

        HttpResponseMessage[] responses = await Task.WhenAll(
            firstPlanner.PutAsync(PlacementPath(LiveVersion, lotA), Placement(plant.Press1, Berlin(16, 8))),
            secondPlanner.PutAsync(PlacementPath(LiveVersion, lotB), Placement(plant.Press1, Berlin(16, 10))));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        await ProblemAssert.HasProblemAsync(Assert.Single(responses, response => response.StatusCode != HttpStatusCode.OK), HttpStatusCode.UnprocessableEntity, "rule.R2.overlap");
        Assert.Single(await StoredPlacementsOnAsync(plant.Press1));
    }

    [Fact]
    public async Task AC_50_cancelling_removes_the_placements_of_unstarted_lots_in_every_version_and_keeps_started_ones()
    {
        TestPlant plant = await CreatePlantAsync("AC50");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-50", plant.ProductId, quantity: LotQuantity));
        int completedLot = LotIds(order)[0];
        int unstartedLot = LotIds(order)[1];
        await PlaceLotAsync(planner, completedLot, plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, unstartedLot, plant.Press1, Berlin(16, 10));
        await AddScenarioPlacementAsync(api.Factory, unstartedLot, plant.Press2, Berlin(17, 8), Berlin(17, 8).AddMinutes(CuringLotMinutes));
        lotActivity.StartedLotIds.Add(completedLot);
        lotActivity.CompletedLotIds.Add(completedLot);

        await CancelOrderAsync(planner, order.GetProperty("id").GetInt32());

        Assert.Equal([completedLot], (await StoredPlacementsOfAsync([completedLot, unstartedLot])).Select(placement => placement.LotId));
    }

    [Fact]
    public async Task AC_161_AC_50_a_cancel_whose_placement_removal_fails_saves_nothing()
    {
        string connectionString = postgres.NewDatabaseConnectionString();
        int orderId;
        int lotId;
        await using (SchedulerApiFactory placing = new(connectionString))
        {
            TestPlant plant = await SchedulingTestData.CreatePlantAsync(placing, "AC161");
            using ApiSession planner = await placing.SignInAsRoleAsync(UserRole.Planner);
            JsonElement created = await CreateOrderAsync(planner, NewOrder("SO-161", plant.ProductId, quantity: LotQuantity));
            orderId = created.GetProperty("id").GetInt32();
            lotId = LotIds(created)[0];
            await PlaceLotAsync(planner, lotId, plant.Builder1, Berlin(16, 8));
        }

        await using SchedulerApiFactory failing = new(
            connectionString,
            replaceServices: services => services.Replace(ServiceDescriptor.Singleton<ILotProgressStates>(new FailingLotProgressStates())));
        using ApiSession failingPlanner = await failing.SignInAsRoleAsync(UserRole.Planner);

        HttpResponseMessage response = await failingPlanner.PostAsync($"{OrderPath(orderId)}/cancel");

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.InternalServerError, ProblemCodes.ServerError);
        JsonElement order = await failingPlanner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal(JsonValueKind.Null, order.GetProperty("cancelledAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, Lot(order, lotId).GetProperty("placement").ValueKind);
    }

    [Fact]
    public async Task AC_20_AC_77_AC_29_a_machine_with_placements_cannot_be_deleted_or_change_type_but_other_fields_can()
    {
        TestPlant plant = await CreatePlantAsync("AC77");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await PlaceLotAsync(planner, LotIds(await CreateOrderAsync(planner, NewOrder("SO-77", plant.ProductId, quantity: LotQuantity)))[1], plant.Press1, Berlin(16, 8));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        string pressPath = $"{ResourcesPath}/{plant.Press1}";

        HttpResponseMessage deletion = await admin.DeleteAsync(pressPath);
        HttpResponseMessage typeChange = await admin.PutAsync(pressPath, new { name = "AC77 Press 1", resourceTypeId = plant.BuildingTypeId, isActive = true });
        HttpResponseMessage otherChanges = await admin.PutAsync(pressPath, new { name = "AC77 Press 1 renamed", resourceTypeId = plant.CuringTypeId, isActive = false });
        HttpResponseMessage typeChangeWithoutPlacements = await admin.PutAsync($"{ResourcesPath}/{plant.Press2}", new { name = "AC77 Press 2", resourceTypeId = plant.BuildingTypeId, isActive = true });

        JsonElement deletionProblem = await ProblemAssert.HasProblemAsync(deletion, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Contains("placement", deletionProblem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        await ProblemAssert.HasProblemAsync(typeChange, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Equal(HttpStatusCode.OK, otherChanges.StatusCode);
        Assert.Equal(HttpStatusCode.OK, typeChangeWithoutPlacements.StatusCode);
        JsonElement press = await admin.GetJsonAsync(pressPath);
        Assert.Equal((plant.CuringTypeId, false), (press.GetProperty("resourceType").GetProperty("id").GetInt32(), press.GetProperty("isActive").GetBoolean()));
    }

    private Task<TestPlant> CreatePlantAsync(string prefix) => SchedulingTestData.CreatePlantAsync(api.Factory, prefix);

    private async Task<List<Placement>> StoredPlacementsOfAsync(int[] lotIds)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Placements
            .Where(placement => lotIds.Contains(placement.LotId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Placement>> StoredPlacementsOnAsync(int resourceId)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Placements
            .Where(placement => placement.ResourceId == resourceId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Placement?> StoredPlacementAsync(int lotId) => Assert.Single(await StoredPlacementsOfAsync([lotId]));

    private async Task<int> PlacementIdAsync(int lotId) => (await StoredPlacementAsync(lotId))!.Id;

    private static void AssertRejection(JsonElement problem, string rule, string conflictType, int conflictId) =>
        Assert.Equal(
            (rule, conflictType, conflictId),
            (problem.GetProperty("rule").GetString(), problem.GetProperty("conflict").GetProperty("type").GetString(), problem.GetProperty("conflict").GetProperty("id").GetInt32()));
}

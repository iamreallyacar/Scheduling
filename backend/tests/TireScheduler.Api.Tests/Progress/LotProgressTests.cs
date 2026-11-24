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

namespace TireScheduler.Api.Tests.Progress;

// F-PRG-1 (acceptance-criteria.md section 9) and the rules of the other modules that follow from
// stored progress (R8, the order rules, the statuses, the late flag and Running), with real
// progress, placements and orders, on the plant default calendar in Europe/Berlin.
public sealed class LotProgressTests : IAsyncDisposable
{
    // Friday afternoon of the week the lots are placed in, so their actual times can lie before now.
    private static readonly DateTimeOffset Now = Berlin(20, 12);

    private readonly FakeTimeProvider clock = new(Now);
    private readonly SchedulerApiFixture api;

    public LotProgressTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_206_AC_59_AC_72_AC_10_a_planner_records_a_start_and_the_lot_and_order_read_in_progress()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC206");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-206", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        int buildingLot = LotIds(order)[0];
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage viewerWrite = await viewer.PutAsync(ProgressPath(buildingLot), ProgressBody(Berlin(16, 8)));
        HttpResponseMessage recorded = await planner.PutAsync(ProgressPath(buildingLot), ProgressBody(Berlin(16, 9)));

        await ProblemAssert.HasProblemAsync(viewerWrite, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        JsonElement body = await ApiSession.ReadJsonAsync(recorded);
        Assert.Equal(buildingLot, body.GetProperty("lotId").GetInt32());
        Assert.Equal(Berlin(16, 9), body.GetProperty("actualStart").GetDateTimeOffset());
        Assert.Equal(TimeSpan.Zero, body.GetProperty("actualStart").GetDateTimeOffset().Offset);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("actualEnd").ValueKind);
        JsonElement detail = await viewer.GetJsonAsync(OrderPath(orderId));
        Assert.Equal("InProgress", detail.GetProperty("status").GetString());
        JsonElement lot = Lot(detail, buildingLot);
        Assert.Equal("InProgress", lot.GetProperty("status").GetString());
        Assert.Equal(Berlin(16, 9), lot.GetProperty("actualStart").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, lot.GetProperty("actualEnd").ValueKind);
        Assert.Equal(JsonValueKind.Null, Lot(detail, LotIds(order)[1]).GetProperty("actualStart").ValueKind);
        Assert.Equal("InProgress", ListedOrder(await viewer.GetJsonAsync($"{OrdersPath}?status=InProgress"), orderId).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC_207_AC_60_a_completed_order_is_late_only_when_its_last_actual_end_is_after_its_due_day_and_progress_can_be_corrected()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC207");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        // Due Tuesday; now is Friday, so the order is late unless it finished in time.
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-207", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-17"));
        int orderId = order.GetProperty("id").GetInt32();
        (int buildingLot, int curingLot) = (LotIds(order)[0], LotIds(order)[1]);
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, curingLot, plant.Press1, Berlin(16, 10));

        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8), Berlin(16, 10));
        JsonElement halfDone = await planner.GetJsonAsync(OrderPath(orderId));
        JsonElement completed = await RecordProgressAsync(planner, curingLot, Berlin(16, 10), Berlin(17, 23, 59));
        JsonElement onTime = await planner.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, curingLot, Berlin(16, 10), Berlin(18, 0, 1));
        JsonElement finishedLate = await planner.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, curingLot, Berlin(16, 11));
        JsonElement reopened = await planner.GetJsonAsync(OrderPath(orderId));

        Assert.Equal("Completed", Lot(halfDone, buildingLot).GetProperty("status").GetString());
        Assert.Equal("InProgress", halfDone.GetProperty("status").GetString());
        Assert.Equal(Berlin(17, 23, 59), completed.GetProperty("actualEnd").GetDateTimeOffset());
        Assert.Equal(("Completed", false), (onTime.GetProperty("status").GetString(), onTime.GetProperty("isLate").GetBoolean()));
        Assert.Equal(Berlin(17, 23, 59), Lot(onTime, curingLot).GetProperty("actualEnd").GetDateTimeOffset());
        Assert.Equal(("Completed", true), (finishedLate.GetProperty("status").GetString(), finishedLate.GetProperty("isLate").GetBoolean()));
        Assert.Equal(("InProgress", true), (reopened.GetProperty("status").GetString(), reopened.GetProperty("isLate").GetBoolean()));
        JsonElement reopenedLot = Lot(reopened, curingLot);
        Assert.Equal(("InProgress", Berlin(16, 11)), (reopenedLot.GetProperty("status").GetString(), reopenedLot.GetProperty("actualStart").GetDateTimeOffset()));
        Assert.Equal(JsonValueKind.Null, reopenedLot.GetProperty("actualEnd").ValueKind);
        Assert.True(ListedOrder(await planner.GetJsonAsync($"{OrdersPath}?late=true"), orderId).GetProperty("isLate").GetBoolean());
    }

    [Fact]
    public async Task AC_208_AC_51_cleared_progress_makes_the_lot_scheduled_again_so_it_can_be_moved_and_its_order_cancelled()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC208");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-208", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        int buildingLot = LotIds(order)[0];
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8));

        HttpResponseMessage cancelWhileRunning = await planner.PostAsync($"{OrderPath(orderId)}/cancel");
        HttpResponseMessage cleared = await planner.DeleteAsync(ProgressPath(buildingLot));
        HttpResponseMessage clearedAgain = await planner.DeleteAsync(ProgressPath(buildingLot));
        JsonElement afterClear = await planner.GetJsonAsync(OrderPath(orderId));
        HttpResponseMessage moved = await planner.PutAsync(PlacementPath(LiveVersion, buildingLot), Placement(plant.Builder1, Berlin(17, 8)));

        await ProblemAssert.HasProblemAsync(cancelWhileRunning, HttpStatusCode.Conflict, ProblemCodes.OrderLotInProgress);
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        await ProblemAssert.HasProblemAsync(clearedAgain, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.Equal(("Scheduled", "Scheduled"), (afterClear.GetProperty("status").GetString(), Lot(afterClear, buildingLot).GetProperty("status").GetString()));
        Assert.Equal(JsonValueKind.Null, Lot(afterClear, buildingLot).GetProperty("actualStart").ValueKind);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal("Cancelled", (await CancelOrderAsync(planner, orderId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC_209_AC_23_unfit_times_are_reported_by_field_and_unknown_lots_are_not_found()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC209");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-209", plant.ProductId, quantity: LotQuantity));
        JsonElement deleted = await CreateOrderAsync(planner, NewOrder("SO-209-X", plant.ProductId, quantity: LotQuantity));
        Assert.Equal(HttpStatusCode.NoContent, (await planner.DeleteAsync(OrderPath(deleted.GetProperty("id").GetInt32()))).StatusCode);
        int buildingLot = LotIds(order)[0];
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        string path = ProgressPath(buildingLot);

        (HttpResponseMessage Response, string Field)[] fieldErrors =
        [
            (await planner.PutAsync(path, new { actualEnd = Berlin(16, 10) }), "actualStart"),
            (await planner.PutAsync(path, ProgressBody(Berlin(16, 8).AddSeconds(30))), "actualStart"),
            (await planner.PutAsync(path, ProgressBody(Berlin(16, 8), Berlin(16, 10).AddSeconds(30))), "actualEnd"),
            (await planner.PutAsync(path, ProgressBody(Berlin(16, 8), Berlin(16, 8))), "actualEnd"),
            (await planner.PutAsync(path, ProgressBody(Berlin(16, 8), Berlin(16, 7))), "actualEnd"),
            (await planner.PutAsync(path, ProgressBody(Now.AddMinutes(1))), "actualStart"),
            (await planner.PutAsync(path, ProgressBody(Berlin(16, 8), Now.AddMinutes(1))), "actualEnd"),
        ];
        HttpResponseMessage startingNow = await planner.PutAsync(path, ProgressBody(Now));
        HttpResponseMessage unknownLot = await planner.PutAsync(ProgressPath(999_999), ProgressBody(Berlin(16, 8)));
        HttpResponseMessage lotOfDeletedOrder = await planner.PutAsync(ProgressPath(LotIds(deleted)[0]), ProgressBody(Berlin(16, 8)));
        HttpResponseMessage unknownLotCleared = await planner.DeleteAsync(ProgressPath(999_999));

        foreach ((HttpResponseMessage response, string field) in fieldErrors)
        {
            JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
            Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"No error for {field}: {problem}");
        }

        Assert.Equal(HttpStatusCode.OK, startingNow.StatusCode);
        await ProblemAssert.HasProblemAsync(unknownLot, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(lotOfDeletedOrder, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(unknownLotCleared, HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_210_a_lot_without_a_live_placement_cannot_start_and_a_cancelled_order_keeps_its_progress()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC210");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-210", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        (int buildingLot, int curingLot) = (LotIds(order)[0], LotIds(order)[1]);
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));

        HttpResponseMessage unplacedStart = await planner.PutAsync(ProgressPath(curingLot), ProgressBody(Berlin(16, 10)));
        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8), Berlin(16, 10));
        await CancelOrderAsync(planner, orderId);
        HttpResponseMessage correction = await planner.PutAsync(ProgressPath(buildingLot), ProgressBody(Berlin(16, 9), Berlin(16, 11)));
        HttpResponseMessage clearing = await planner.DeleteAsync(ProgressPath(buildingLot));
        HttpResponseMessage cancelledStart = await planner.PutAsync(ProgressPath(curingLot), ProgressBody(Berlin(16, 10)));

        await ProblemAssert.HasProblemAsync(unplacedStart, HttpStatusCode.Conflict, "progress.not_placed");
        await ProblemAssert.HasProblemAsync(correction, HttpStatusCode.Conflict, ProblemCodes.OrderCancelled);
        await ProblemAssert.HasProblemAsync(clearing, HttpStatusCode.Conflict, ProblemCodes.OrderCancelled);
        await ProblemAssert.HasProblemAsync(cancelledStart, HttpStatusCode.Conflict, ProblemCodes.OrderCancelled);
        JsonElement detail = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal(("Completed", Berlin(16, 8), Berlin(16, 10)), LotProgressValues(Lot(detail, buildingLot)));
        Assert.Equal("Cancelled", Lot(detail, curingLot).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, Lot(detail, curingLot).GetProperty("actualStart").ValueKind);
    }

    [Fact]
    public async Task AC_211_a_start_and_a_placement_removal_or_a_cancel_at_the_same_moment_run_one_after_the_other()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC211");
        using ApiSession firstPlanner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        using ApiSession secondPlanner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement removalOrder = await CreateOrderAsync(firstPlanner, NewOrder("SO-211-A", plant.ProductId, quantity: LotQuantity));
        JsonElement cancelOrder = await CreateOrderAsync(firstPlanner, NewOrder("SO-211-B", plant.ProductId, quantity: LotQuantity));
        int removalLot = LotIds(removalOrder)[0];
        int cancelLot = LotIds(cancelOrder)[0];
        int cancelOrderId = cancelOrder.GetProperty("id").GetInt32();
        await PlaceLotAsync(firstPlanner, removalLot, plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(firstPlanner, cancelLot, plant.Builder1, Berlin(16, 10));

        HttpResponseMessage[] startAndRemoval = await Task.WhenAll(
            firstPlanner.PutAsync(ProgressPath(removalLot), ProgressBody(Berlin(16, 8))),
            secondPlanner.DeleteAsync(PlacementPath(LiveVersion, removalLot)));
        HttpResponseMessage[] startAndCancel = await Task.WhenAll(
            firstPlanner.PutAsync(ProgressPath(cancelLot), ProgressBody(Berlin(16, 10))),
            secondPlanner.PostAsync($"{OrderPath(cancelOrderId)}/cancel"));

        JsonElement removalLotRead = Lot(await firstPlanner.GetJsonAsync(OrderPath(removalOrder.GetProperty("id").GetInt32())), removalLot);
        if (startAndRemoval[0].StatusCode == HttpStatusCode.OK)
        {
            await ProblemAssert.HasProblemAsync(startAndRemoval[1], HttpStatusCode.UnprocessableEntity, "rule.R8.started");
            Assert.Equal("InProgress", removalLotRead.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Object, removalLotRead.GetProperty("placement").ValueKind);
        }
        else
        {
            await ProblemAssert.HasProblemAsync(startAndRemoval[0], HttpStatusCode.Conflict, "progress.not_placed");
            Assert.Equal(HttpStatusCode.NoContent, startAndRemoval[1].StatusCode);
            Assert.Equal("Unscheduled", removalLotRead.GetProperty("status").GetString());
        }

        JsonElement cancelOrderRead = await firstPlanner.GetJsonAsync(OrderPath(cancelOrderId));
        if (startAndCancel[0].StatusCode == HttpStatusCode.OK)
        {
            await ProblemAssert.HasProblemAsync(startAndCancel[1], HttpStatusCode.Conflict, ProblemCodes.OrderLotInProgress);
            Assert.Equal("InProgress", cancelOrderRead.GetProperty("status").GetString());
        }
        else
        {
            await ProblemAssert.HasProblemAsync(startAndCancel[0], HttpStatusCode.Conflict, ProblemCodes.OrderCancelled);
            Assert.Equal(HttpStatusCode.NoContent, startAndCancel[1].StatusCode);
            Assert.Equal("Cancelled", cancelOrderRead.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, Lot(cancelOrderRead, cancelLot).GetProperty("actualStart").ValueKind);
        }
    }

    [Fact]
    public async Task AC_19_AC_49_AC_51_an_order_with_a_started_lot_cannot_be_deleted_changed_or_cancelled()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC19");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-19", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        await PlaceLotAsync(planner, LotIds(order)[0], plant.Builder1, Berlin(16, 8));
        await RecordProgressAsync(planner, LotIds(order)[0], Berlin(16, 8));

        HttpResponseMessage deletion = await planner.DeleteAsync(OrderPath(orderId));
        HttpResponseMessage quantityChange = await planner.PutAsync(OrderPath(orderId), NewOrder("SO-19", plant.ProductId, quantity: LotQuantity + 1));
        HttpResponseMessage cancel = await planner.PostAsync($"{OrderPath(orderId)}/cancel");

        await ProblemAssert.HasProblemAsync(deletion, HttpStatusCode.Conflict, ProblemCodes.OrderNotDeletable);
        await ProblemAssert.HasProblemAsync(quantityChange, HttpStatusCode.Conflict, ProblemCodes.OrderLocked);
        await ProblemAssert.HasProblemAsync(cancel, HttpStatusCode.Conflict, ProblemCodes.OrderLotInProgress);
        Assert.Equal("InProgress", (await planner.GetJsonAsync(OrderPath(orderId))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC_112_AC_113_AC_50_a_started_lot_keeps_its_placement_and_a_cancel_removes_only_unstarted_ones()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC112");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-112", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        (int buildingLot, int curingLot) = (LotIds(order)[0], LotIds(order)[1]);
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, curingLot, plant.Press1, Berlin(16, 10));
        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8), Berlin(16, 10));
        await RecordProgressAsync(planner, curingLot, Berlin(16, 10));

        HttpResponseMessage[] rejected =
        [
            // Earlier, so the move breaks no rule but R8.
            await planner.PutAsync(PlacementPath(LiveVersion, buildingLot), Placement(plant.Builder1, Berlin(16, 6))),
            await planner.DeleteAsync(PlacementPath(LiveVersion, buildingLot)),
            await planner.PutAsync(PlacementPath(LiveVersion, curingLot), Placement(plant.Press2, Berlin(16, 10))),
            await planner.DeleteAsync(PlacementPath(LiveVersion, curingLot)),
        ];
        foreach (HttpResponseMessage response in rejected)
        {
            await ProblemAssert.HasProblemAsync(response, HttpStatusCode.UnprocessableEntity, "rule.R8.started");
        }

        // Once the curing lot is completed too, the order can be cancelled and both placements stay.
        await RecordProgressAsync(planner, curingLot, Berlin(16, 10), Berlin(16, 14));
        JsonElement cancelled = await CancelOrderAsync(planner, orderId);
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(Berlin(16, 8), Lot(cancelled, buildingLot).GetProperty("placement").GetProperty("plannedStart").GetDateTimeOffset());
        Assert.Equal(Berlin(16, 10), Lot(cancelled, curingLot).GetProperty("placement").GetProperty("plannedStart").GetDateTimeOffset());

        JsonElement other = await CreateOrderAsync(planner, NewOrder("SO-50", plant.ProductId, quantity: LotQuantity));
        await PlaceLotAsync(planner, LotIds(other)[0], plant.Builder1, Berlin(17, 8));
        await PlaceLotAsync(planner, LotIds(other)[1], plant.Press1, Berlin(17, 10));
        await RecordProgressAsync(planner, LotIds(other)[0], Berlin(17, 8), Berlin(17, 10));
        JsonElement otherCancelled = await CancelOrderAsync(planner, other.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Object, Lot(otherCancelled, LotIds(other)[0]).GetProperty("placement").ValueKind);
        Assert.Equal(JsonValueKind.Null, Lot(otherCancelled, LotIds(other)[1]).GetProperty("placement").ValueKind);
    }

    [Fact]
    public async Task AC_214_AC_185_a_machine_with_a_lot_in_progress_is_running_unless_an_unavailability_covers_now()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC214");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement first = await CreateOrderAsync(planner, NewOrder("SO-214-A", plant.ProductId, quantity: LotQuantity));
        JsonElement second = await CreateOrderAsync(planner, NewOrder("SO-214-B", plant.ProductId, quantity: LotQuantity));
        // Both lots were planned for Monday; now is Friday, outside their planned times.
        await PlaceLotAsync(planner, LotIds(first)[1], plant.Press1, Berlin(16, 8));
        await PlaceLotAsync(planner, LotIds(second)[1], plant.Press2, Berlin(16, 8));
        await RecordProgressAsync(planner, LotIds(first)[1], Berlin(16, 8));
        await RecordProgressAsync(planner, LotIds(second)[1], Berlin(16, 8));
        await CreateUnavailabilityAsync(planner, plant.Press2, Unavailability("Breakdown", Now.AddHours(-1), end: null));

        Dictionary<int, string> statusById = (await planner.GetJsonAsync(ResourcesPath)).EnumerateArray()
            .ToDictionary(machine => machine.GetProperty("id").GetInt32(), machine => machine.GetProperty("status").GetString()!);
        JsonElement timeline = await planner.GetJsonAsync(TimelinePath(LiveVersion, "from=2026-11-16&to=2026-11-29"));
        JsonElement timelinePress1 = timeline.GetProperty("resourceTypes").EnumerateArray()
            .SelectMany(type => type.GetProperty("resources").EnumerateArray())
            .Single(machine => machine.GetProperty("id").GetInt32() == plant.Press1);

        Assert.Equal(("Running", "Down", "Idle"), (statusById[plant.Press1], statusById[plant.Press2], statusById[plant.Builder1]));
        Assert.Equal("Running", (await planner.GetJsonAsync($"{ResourcesPath}/{plant.Press1}")).GetProperty("status").GetString());
        Assert.Equal("Running", timelinePress1.GetProperty("status").GetString());
        Assert.Equal("InProgress", Assert.Single(timelinePress1.GetProperty("placements").EnumerateArray()).GetProperty("status").GetString());

        await RecordProgressAsync(planner, LotIds(first)[1], Berlin(16, 8), Berlin(16, 12));
        Assert.Equal("Idle", (await planner.GetJsonAsync($"{ResourcesPath}/{plant.Press1}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC_182_AC_183_A36_a_completed_lot_keeps_its_planned_end_and_is_never_listed_as_a_conflict()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC182");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement completedOrder = await CreateOrderAsync(planner, NewOrder("SO-182-A", plant.ProductId, quantity: LotQuantity));
        JsonElement runningOrder = await CreateOrderAsync(planner, NewOrder("SO-182-B", plant.ProductId, quantity: LotQuantity));
        JsonElement waitingOrder = await CreateOrderAsync(planner, NewOrder("SO-182-C", plant.ProductId, quantity: LotQuantity));
        (int completedLot, int runningLot, int waitingLot) = (LotIds(completedOrder)[1], LotIds(runningOrder)[1], LotIds(waitingOrder)[1]);
        // From Fri 22:00 the curing lots pause over the weekend and end Mon 02:00 (R6). Their actual
        // times came earlier than planned (A47).
        await PlaceLotAsync(planner, completedLot, plant.Press1, Berlin(20, 22));
        await PlaceLotAsync(planner, runningLot, plant.Press2, Berlin(20, 22));
        await PlaceLotAsync(planner, waitingLot, plant.Press1, Berlin(23, 8));
        await RecordProgressAsync(planner, completedLot, Berlin(19, 8), Berlin(19, 12));
        await RecordProgressAsync(planner, runningLot, Berlin(20, 8));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        await CreateHolidayAsync(admin, "2026-11-23", "AC182 holiday");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press1}", new { name = "AC182 Press 1", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press2}", new { name = "AC182 Press 2", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);

        Assert.Equal(Berlin(23, 2), Lot(await planner.GetJsonAsync(OrderPath(completedOrder.GetProperty("id").GetInt32())), completedLot).GetProperty("placement").GetProperty("plannedEnd").GetDateTimeOffset());
        Assert.Equal(Berlin(24, 2), Lot(await planner.GetJsonAsync(OrderPath(runningOrder.GetProperty("id").GetInt32())), runningLot).GetProperty("placement").GetProperty("plannedEnd").GetDateTimeOffset());
        JsonElement conflicts = await planner.GetJsonAsync(ConflictsPath(LiveVersion));
        Assert.Equal(
            [(runningLot, "InProgress"), (waitingLot, "Scheduled")],
            conflicts.EnumerateArray().Select(conflict => (conflict.GetProperty("lotId").GetInt32(), conflict.GetProperty("status").GetString())).ToArray());
    }

    [Fact]
    public async Task AC_199_a_completed_order_would_be_late_only_when_it_finished_after_its_due_day()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC199");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        // Due Tuesday; now is Friday, so only the completion time can make it on time.
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-199", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-17"));
        int orderId = order.GetProperty("id").GetInt32();
        await PlaceLotAsync(planner, LotIds(order)[0], plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, LotIds(order)[1], plant.Press1, Berlin(16, 10));
        await RecordProgressAsync(planner, LotIds(order)[0], Berlin(16, 8), Berlin(16, 10));
        await RecordProgressAsync(planner, LotIds(order)[1], Berlin(16, 10), Berlin(16, 14));

        HttpResponseMessage proposal = await planner.PostAsync(AsapProposalPath(LiveVersion, orderId, notBefore: null));

        Assert.Equal(HttpStatusCode.OK, proposal.StatusCode);
        JsonElement body = await ApiSession.ReadJsonAsync(proposal);
        Assert.All(body.GetProperty("lots").EnumerateArray(), lot => Assert.False(lot.GetProperty("isProposed").GetBoolean()));
        Assert.False(body.GetProperty("wouldBeLate").GetBoolean());
    }

    private static (string? Status, DateTimeOffset ActualStart, DateTimeOffset ActualEnd) LotProgressValues(JsonElement lot) =>
        (lot.GetProperty("status").GetString(), lot.GetProperty("actualStart").GetDateTimeOffset(), lot.GetProperty("actualEnd").GetDateTimeOffset());
}

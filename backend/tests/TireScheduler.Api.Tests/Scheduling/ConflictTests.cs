using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.Fakes;
using TireScheduler.Api.Tests.TestData;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Scheduling;

// F-SCH-4: later facts keep the placements, recompute their planned ends and list the ones that
// now break a rule (DEC-18, acceptance-criteria.md section 8), on the plant default calendar in
// Europe/Berlin.
public sealed class ConflictTests : IAsyncDisposable
{
    private static readonly string[] TuesdayToFriday = ["Tuesday", "Wednesday", "Thursday", "Friday"];

    private readonly PostgresFixture postgres;

    // Before every due date of these tests, so only the planned ends make an order late.
    private readonly FakeTimeProvider clock = new(Berlin(16, 1));

    private readonly SchedulerApiFixture api;

    public ConflictTests(PostgresFixture postgres)
    {
        this.postgres = postgres;
        api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_182_AC_107_a_holiday_moves_the_planned_ends_in_every_version_and_deleting_it_moves_them_back()
    {
        TestPlant plant = await CreatePlantAsync("AC107");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement orderA = await CreateOrderAsync(planner, NewOrder("SO-107-A", plant.ProductId, quantity: LotQuantity, dueDate: "2026-11-23"));
        int orderAId = orderA.GetProperty("id").GetInt32();
        int lotA = LotIds(orderA)[1];
        int lotB = LotIds(await CreateOrderAsync(planner, NewOrder("SO-107-B", plant.ProductId, quantity: LotQuantity)))[1];
        // From Fri 22:00 the curing lot pauses over the weekend and ends Mon 02:00; lot B starts Tue 00:00.
        await PlaceLotAsync(planner, lotA, plant.Press1, Berlin(20, 22));
        await PlaceLotAsync(planner, lotB, plant.Press1, Berlin(24, 0));
        int scenarioId = await AddScenarioPlacementAsync(api.Factory, lotA, plant.Press2, Berlin(20, 22), Berlin(23, 2));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        int holidayId = await CreateHolidayAsync(admin, "2026-11-23", "AC107 holiday");

        Assert.Equal(Berlin(24, 2), await LivePlannedEndAsync(planner, orderAId, lotA));
        Assert.Equal(Berlin(24, 2), (await StoredPlacementAsync(scenarioId, lotA)).PlannedEnd);
        Assert.True((await planner.GetJsonAsync(OrderPath(orderAId))).GetProperty("isLate").GetBoolean());
        JsonElement conflicts = await planner.GetJsonAsync(ConflictsPath(LiveVersion));
        Assert.Equal([lotA, lotB], ConflictedLotIds(conflicts));
        Assert.All(conflicts.EnumerateArray(), conflict => Assert.Equal("rule.R2.overlap", Assert.Single(conflict.GetProperty("violations").EnumerateArray()).GetProperty("code").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{HolidaysPath}/{holidayId}")).StatusCode);

        Assert.Equal(Berlin(23, 2), await LivePlannedEndAsync(planner, orderAId, lotA));
        Assert.False((await planner.GetJsonAsync(OrderPath(orderAId))).GetProperty("isLate").GetBoolean());
        Assert.Empty((await planner.GetJsonAsync(ConflictsPath(LiveVersion))).EnumerateArray());
    }

    // A working time change recomputes the planned ends of every machine, so it locks every machine
    // first (DEC-44); a placement saved meanwhile on a machine it had not locked would keep an end
    // computed from the old working time.
    [Theory]
    [InlineData(WorkingTimeWrite.AddHoliday)]
    [InlineData(WorkingTimeWrite.DeleteHoliday)]
    [InlineData(WorkingTimeWrite.SaveShifts)]
    public async Task AC_182_a_working_time_change_waits_while_another_command_holds_a_machine_with_a_placement(WorkingTimeWrite workingTimeWrite)
    {
        TestPlant plant = await CreatePlantAsync("AC182L");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await PlaceLotAsync(planner, LotIds(await CreateOrderAsync(planner, NewOrder("SO-182-L", plant.ProductId, quantity: LotQuantity)))[1], plant.Press1, Berlin(16, 8));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);
        int holidayId = await CreateHolidayAsync(admin, "2026-11-24", "AC182L holiday");

        await using HeldRowLock heldPress = await HeldRowLock.TakeAsync<MasterDataDbContext, Resource>(api.Factory, plant.Press1);
        Task<HttpResponseMessage> write = workingTimeWrite switch
        {
            WorkingTimeWrite.AddHoliday => admin.PostAsync(HolidaysPath, new { date = "2026-11-25", name = "AC182L second holiday" }),
            WorkingTimeWrite.DeleteHoliday => admin.DeleteAsync($"{HolidaysPath}/{holidayId}"),
            WorkingTimeWrite.SaveShifts => admin.PutAsync(ShiftsPath(calendarId), new { shifts = new[] { Shift("Monday", "00:00", "24:00") } }),
            _ => throw new ArgumentOutOfRangeException(nameof(workingTimeWrite), workingTimeWrite, "No request for this write."),
        };
        await heldPress.ReleaseOnceItWaitsAsync(write);
        HttpResponseMessage response = await write;

        Assert.True(response.IsSuccessStatusCode, $"The {workingTimeWrite} answered {response.StatusCode} after the machine was released.");
    }

    public enum WorkingTimeWrite
    {
        AddHoliday,
        DeleteHoliday,
        SaveShifts,
    }

    // Removing a placement or cancelling its order locks only the order, so a working time change
    // can read a placement that such a command deletes before the change updates its end. The held
    // placement row stands for that command: its delete is open when the holiday save reads, and
    // commits while the save waits to update the row.
    [Fact]
    public async Task AC_182_AC_25_a_holiday_save_succeeds_when_a_placement_it_recomputes_is_deleted_meanwhile()
    {
        TestPlant plant = await CreatePlantAsync("AC182D");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-182-D", plant.ProductId, quantity: LotQuantity));
        int lotId = LotIds(order)[1];
        // From Fri 22:00 the curing lot ends Mon 02:00, so a Monday holiday moves its end.
        await PlaceLotAsync(planner, lotId, plant.Press1, Berlin(20, 22));
        int placementId = (await StoredPlacementAsync(await LiveVersionIdAsync(), lotId)).Id;
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        await using HeldRowLock heldPlacement = await HeldRowLock.TakeAsync<SchedulingDbContext, Placement>(api.Factory, placementId);
        await heldPlacement.ChangeWhileHeldAsync<SchedulingDbContext>((context, cancellationToken) =>
            context.Placements.Where(placement => placement.Id == placementId).ExecuteDeleteAsync(cancellationToken));
        Task<HttpResponseMessage> write = admin.PostAsync(HolidaysPath, new { date = "2026-11-23", name = "AC182D holiday" });
        await heldPlacement.ReleaseOnceItWaitsAsync(write);
        HttpResponseMessage response = await write;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("AC182D holiday", (await admin.GetJsonAsync(HolidaysPath)).EnumerateArray().Select(holiday => holiday.GetProperty("name").GetString()));
        Assert.Equal(JsonValueKind.Null, Lot(await planner.GetJsonAsync(OrderPath(order.GetProperty("id").GetInt32())), lotId).GetProperty("placement").ValueKind);
    }

    [Fact]
    public async Task AC_182_AC_108_new_shifts_move_a_planned_end_and_a_start_outside_them_keeps_its_end_and_is_listed_under_r6()
    {
        TestPlant plant = await CreatePlantAsync("AC108");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-108-A", plant.ProductId, quantity: LotQuantity));
        int movedLot = LotIds(order)[1];
        int startLeftLot = LotIds(await CreateOrderAsync(planner, NewOrder("SO-108-B", plant.ProductId, quantity: LotQuantity)))[1];
        await PlaceLotAsync(planner, movedLot, plant.Press1, Berlin(16, 14));
        await PlaceLotAsync(planner, startLeftLot, plant.Press2, Berlin(16, 6));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        // Monday keeps only 08:00-16:00; Tuesday to Friday keep three shifts.
        await SaveShiftsAsync(admin, await PlantCalendarIdAsync(admin),
        [
            Shift("Monday", "08:00", "16:00"),
            .. TuesdayToFriday.SelectMany(day => new[] { Shift(day, "00:00", "08:00"), Shift(day, "08:00", "16:00"), Shift(day, "16:00", "24:00") }),
        ]);

        Assert.Equal(Berlin(17, 2), await LivePlannedEndAsync(planner, order.GetProperty("id").GetInt32(), movedLot));
        Assert.Equal(Berlin(16, 6).AddMinutes(CuringLotMinutes), (await StoredPlacementAsync(await LiveVersionIdAsync(), startLeftLot)).PlannedEnd);
        JsonElement conflict = Assert.Single((await planner.GetJsonAsync(ConflictsPath(LiveVersion))).EnumerateArray());
        Assert.Equal(startLeftLot, conflict.GetProperty("lotId").GetInt32());
        JsonElement violation = Assert.Single(conflict.GetProperty("violations").EnumerateArray());
        Assert.Equal(("R6", "rule.R6.outside_working_time", "resource", plant.Press2), ViolationValues(violation));
    }

    [Fact]
    public async Task AC_182_a_shift_or_holiday_change_whose_recompute_fails_saves_nothing()
    {
        string connectionString = postgres.NewDatabaseConnectionString();
        int calendarId;
        int holidayId;
        await using (SchedulerApiFactory setup = new(connectionString))
        {
            using ApiSession setupAdmin = await setup.SignInAsRoleAsync(UserRole.Admin);
            calendarId = await PlantCalendarIdAsync(setupAdmin);
            holidayId = await CreateHolidayAsync(setupAdmin, "2026-12-25", "Christmas");
        }

        await using SchedulerApiFactory failing = new(
            connectionString,
            replaceServices: services => services.Replace(ServiceDescriptor.Scoped<IPlannedEndRecompute, FailingPlannedEndRecompute>()));
        using ApiSession admin = await failing.SignInAsRoleAsync(UserRole.Admin);
        JsonElement shiftsBefore = Assert.Single((await admin.GetJsonAsync(CalendarsPath)).EnumerateArray()).GetProperty("shifts");

        HttpResponseMessage shiftSave = await admin.PutAsync(ShiftsPath(calendarId), new { shifts = new[] { Shift("Monday", "08:00", "16:00") } });
        HttpResponseMessage holidayCreate = await admin.PostAsync(HolidaysPath, new { date = "2026-12-24", name = "Christmas Eve" });
        HttpResponseMessage holidayDelete = await admin.DeleteAsync($"{HolidaysPath}/{holidayId}");

        await ProblemAssert.HasProblemAsync(shiftSave, HttpStatusCode.InternalServerError, ProblemCodes.ServerError);
        await ProblemAssert.HasProblemAsync(holidayCreate, HttpStatusCode.InternalServerError, ProblemCodes.ServerError);
        await ProblemAssert.HasProblemAsync(holidayDelete, HttpStatusCode.InternalServerError, ProblemCodes.ServerError);
        Assert.Equal(shiftsBefore.GetRawText(), Assert.Single((await admin.GetJsonAsync(CalendarsPath)).EnumerateArray()).GetProperty("shifts").GetRawText());
        Assert.Equal([holidayId], (await admin.GetJsonAsync(HolidaysPath)).EnumerateArray().Select(holiday => holiday.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task AC_183_AC_88_AC_76_every_role_reads_the_placements_a_breakdown_and_a_deactivation_made_conflict()
    {
        TestPlant plant = await CreatePlantAsync("AC183");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement brokenDownOrder = await CreateOrderAsync(planner, NewOrder("SO-183-A", plant.ProductId, quantity: LotQuantity));
        int brokenDownLot = LotIds(brokenDownOrder)[1];
        int deactivatedLot = LotIds(await CreateOrderAsync(planner, NewOrder("SO-183-B", plant.ProductId, quantity: LotQuantity)))[1];
        await PlaceLotAsync(planner, brokenDownLot, plant.Press1, Berlin(17, 6));
        await PlaceLotAsync(planner, deactivatedLot, plant.Press2, Berlin(17, 6));
        Assert.Empty((await planner.GetJsonAsync(ConflictsPath(LiveVersion))).EnumerateArray());

        int breakdownId = await CreateUnavailabilityAsync(planner, plant.Press1, Unavailability("Breakdown", Berlin(17, 8), end: null));
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"{ResourcesPath}/{plant.Press2}", new { name = "AC183 Press 2", resourceTypeId = plant.CuringTypeId, isActive = false })).StatusCode);

        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);
        JsonElement conflicts = await viewer.GetJsonAsync(ConflictsPath(LiveVersion));

        // Both start Tue 06:00, so the earlier placement id comes first.
        Assert.Equal([brokenDownLot, deactivatedLot], ConflictedLotIds(conflicts));
        JsonElement brokenDown = conflicts[0];
        int liveVersionId = await LiveVersionIdAsync();
        Assert.Equal((await StoredPlacementAsync(liveVersionId, brokenDownLot)).Id, brokenDown.GetProperty("placementId").GetInt32());
        Assert.Equal((Berlin(17, 6), Berlin(17, 6).AddMinutes(CuringLotMinutes)), (brokenDown.GetProperty("plannedStart").GetDateTimeOffset(), brokenDown.GetProperty("plannedEnd").GetDateTimeOffset()));
        Assert.Equal((plant.Press1, "AC183 Press 1"), Reference(brokenDown.GetProperty("resource")));
        Assert.Equal((1, LotQuantity, CuringLotMinutes), (brokenDown.GetProperty("lotNo").GetInt32(), brokenDown.GetProperty("quantity").GetInt32(), brokenDown.GetProperty("durationMinutes").GetInt32()));
        Assert.Equal((brokenDownOrder.GetProperty("id").GetInt32(), "SO-183-A"), (brokenDown.GetProperty("orderId").GetInt32(), brokenDown.GetProperty("orderNo").GetString()));
        Assert.Equal((2, plant.CuringTypeId, "AC183 curing"), (brokenDown.GetProperty("sequenceNo").GetInt32(), Reference(brokenDown.GetProperty("resourceType")).Id, Reference(brokenDown.GetProperty("resourceType")).Name));
        JsonElement breakdownViolation = Assert.Single(brokenDown.GetProperty("violations").EnumerateArray());
        Assert.Equal(("R3", "rule.R3.unavailable", "unavailability", breakdownId), ViolationValues(breakdownViolation));
        Assert.False(string.IsNullOrWhiteSpace(breakdownViolation.GetProperty("detail").GetString()));
        Assert.Equal(JsonValueKind.Null, breakdownViolation.GetProperty("earliestAllowedStart").ValueKind);
        Assert.Equal((plant.Press2, "AC183 Press 2"), Reference(conflicts[1].GetProperty("resource")));
        JsonElement inactiveViolation = Assert.Single(conflicts[1].GetProperty("violations").EnumerateArray());
        Assert.Equal(("R1", "rule.R1.inactive", "resource", plant.Press2), ViolationValues(inactiveViolation));
    }

    [Fact]
    public async Task AC_183_an_unknown_version_is_not_found()
    {
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        await ProblemAssert.HasProblemAsync(await viewer.GetAsync(ConflictsPath("999")), HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    private Task<TestPlant> CreatePlantAsync(string prefix) => SchedulingTestData.CreatePlantAsync(api.Factory, prefix);

    private static async Task<DateTimeOffset> LivePlannedEndAsync(ApiSession session, int orderId, int lotId)
    {
        JsonElement order = await session.GetJsonAsync(OrderPath(orderId));
        return Lot(order, lotId).GetProperty("placement").GetProperty("plannedEnd").GetDateTimeOffset();
    }

    private async Task<int> LiveVersionIdAsync()
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().ScheduleVersions
            .Where(version => version.IsLive)
            .Select(version => version.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Placement> StoredPlacementAsync(int versionId, int lotId)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Placements
            .SingleAsync(placement => placement.VersionId == versionId && placement.LotId == lotId, TestContext.Current.CancellationToken);
    }

    private static int[] ConflictedLotIds(JsonElement conflicts) => [.. conflicts.EnumerateArray().Select(conflict => conflict.GetProperty("lotId").GetInt32())];

    private static (string? Rule, string? Code, string? ConflictType, int ConflictId) ViolationValues(JsonElement violation) =>
        (violation.GetProperty("rule").GetString(),
            violation.GetProperty("code").GetString(),
            violation.GetProperty("conflict").GetProperty("type").GetString(),
            violation.GetProperty("conflict").GetProperty("id").GetInt32());

    private static (int Id, string? Name) Reference(JsonElement reference) =>
        (reference.GetProperty("id").GetInt32(), reference.GetProperty("name").GetString());
}

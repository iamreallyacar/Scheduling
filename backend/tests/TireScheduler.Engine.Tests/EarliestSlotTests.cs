using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

public sealed class EarliestSlotTests
{
    private const int LotA = 101;
    private const int LotB = 102;
    private const int LotC = 103;
    private const int BuildingLot = 104;
    private const int BuildingOperation = 304;
    private const int PlacementOfLotA = 201;
    private const int PlacementOfLotC = 203;
    private const int CuringLotMinutes = 240;
    private const int GapCalendarId = 2;
    private const int Press0 = 14;
    private const int Press9 = 15;

    private static readonly TimeSpan Horizon = TimeSpan.FromDays(90);

    [Fact]
    public void AC_117_on_one_resource_the_slot_starts_when_the_placed_lot_ends()
    {
        ScheduleSnapshot snapshot = WithLotAOnPress1();

        EarliestSlotResult result = Find(snapshot, LotB, [Press1], Mon(8));

        Assert.Equal(new FoundSlot(Press1, Mon(12), Mon(16)), result.Slot);
    }

    [Fact]
    public void AC_118_across_resources_the_slot_with_the_earliest_end_wins()
    {
        ScheduleSnapshot snapshot = WithLotAOnPress1();

        EarliestSlotResult result = Find(snapshot, LotB, resourceIds: null, Mon(8));

        Assert.Equal(new FoundSlot(Press2, Mon(8), Mon(12)), result.Slot);
    }

    [Fact]
    public void AC_118_on_a_tie_of_end_and_start_the_resource_name_decides_not_the_id()
    {
        ScheduleSnapshot plant = Empty().WithLot(LotB, CuringLotMinutes);
        ScheduleSnapshot snapshot = plant with
        {
            Resources = [.. plant.Resources, new SnapshotResource(Press0, "Press 0", CuringType, PlantCalendarId, IsActive: true)],
        };

        EarliestSlotResult result = Find(snapshot, LotB, resourceIds: null, Mon(8));

        Assert.Equal(new FoundSlot(Press0, Mon(8), Mon(12)), result.Slot);
    }

    [Fact]
    public void AC_118_on_a_tie_of_end_the_earlier_start_wins_before_the_name()
    {
        // Press 9 pauses from 08:00 to 10:00, so a 4-hour lot from 06:00 ends at 12:00, as one
        // from 08:00 does on the busy presses.
        SnapshotShift[] shiftsWithGap = [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .SelectMany(weekday => new[] { new SnapshotShift(weekday, 0, 480), new SnapshotShift(weekday, 600, 1440) })];
        ScheduleSnapshot plant = WithLotAOnPress1()
            .WithLot(LotC, CuringLotMinutes)
            .WithPlacement(PlacementOfLotC, LotC, Press2, Mon(4), Mon(8));
        ScheduleSnapshot snapshot = plant with
        {
            Calendars = [.. plant.Calendars, new SnapshotCalendar(GapCalendarId, shiftsWithGap)],
            Resources = [.. plant.Resources, new SnapshotResource(Press9, "Press 9", CuringType, GapCalendarId, IsActive: true)],
            Placements = [.. plant.Placements.Select(placement => placement.LotId == LotA ? placement with { PlannedStart = Mon(4), PlannedEnd = Mon(8) } : placement)],
        };

        EarliestSlotResult result = Find(snapshot, LotB, resourceIds: null, Mon(6));

        Assert.Equal(new FoundSlot(Press9, Mon(6), Mon(12)), result.Slot);
    }

    [Fact]
    public void AC_119_a_not_before_on_the_weekend_gives_the_start_of_the_next_shift()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotB, CuringLotMinutes);

        EarliestSlotResult result = Find(snapshot, LotB, [Press1], Sat(10));

        Assert.Equal(new FoundSlot(Press1, NextMon(0), NextMon(4)), result.Slot);
    }

    [Fact]
    public void AC_120_the_slot_waits_for_the_placed_predecessor_and_its_lag()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(BuildingLot, CuringLotMinutes, BuildingType, operationId: BuildingOperation)
            .WithLot(LotB, CuringLotMinutes)
            .WithPlacement(PlacementOfLotA, BuildingLot, Builder1, Mon(6), Mon(10))
            .WithDependency(BuildingOperation, LotB, minLagMinutes: 120);

        EarliestSlotResult result = Find(snapshot, LotB, resourceIds: null, Mon(8));

        Assert.Equal(new FoundSlot(Press1, Mon(12), Mon(16)), result.Slot);
    }

    [Fact]
    public void AC_121_the_lots_own_placement_is_ignored()
    {
        ScheduleSnapshot snapshot = WithLotAOnPress1();

        EarliestSlotResult result = Find(snapshot, LotA, [Press1], Mon(8));

        Assert.Equal(new FoundSlot(Press1, Mon(8), Mon(12)), result.Slot);
    }

    [Fact]
    public void AC_122_no_slot_when_one_press_is_inactive_and_the_other_broken_down_without_end()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotB, CuringLotMinutes)
            .WithInactive(Press2)
            .WithUnavailability(1, Press1, Mon(8), end: null);

        EarliestSlotResult result = Find(snapshot, LotB, resourceIds: null, Mon(8));

        Assert.Null(result.Slot);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void AC_123_the_search_stops_at_the_horizon()
    {
        DateTimeOffset bookedUntil = Mon(8).AddDays(91);
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, CuringLotMinutes)
            .WithLot(LotB, CuringLotMinutes)
            .WithLot(LotC, CuringLotMinutes)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), bookedUntil)
            .WithPlacement(PlacementOfLotC, LotC, Press2, Mon(8), bookedUntil);

        EarliestSlotResult withinTheHorizon = EarliestSlot.Find(snapshot, new EarliestSlotRequest(LotB, null, Mon(8), Horizon));
        EarliestSlotResult withinALongerHorizon = EarliestSlot.Find(snapshot, new EarliestSlotRequest(LotB, null, Mon(8), TimeSpan.FromDays(100)));

        Assert.Null(withinTheHorizon.Slot);
        Assert.Empty(withinTheHorizon.Violations);
        Assert.Equal(new FoundSlot(Press1, bookedUntil, bookedUntil.AddMinutes(CuringLotMinutes)), withinALongerHorizon.Slot);
    }

    [Fact]
    public void AC_123_a_slot_must_start_before_the_horizon_ends_and_may_end_after_it()
    {
        TimeSpan horizon = TimeSpan.FromDays(91);
        DateTimeOffset bookedUntil = Mon(8) + horizon;
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, CuringLotMinutes)
            .WithLot(LotB, CuringLotMinutes)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), bookedUntil);

        EarliestSlotResult startingAtTheEnd = EarliestSlot.Find(snapshot, new EarliestSlotRequest(LotB, [Press1], Mon(8), horizon));
        EarliestSlotResult startingInTheLastMinute = EarliestSlot.Find(snapshot, new EarliestSlotRequest(LotB, [Press1], Mon(8), horizon + TimeSpan.FromMinutes(1)));

        Assert.Null(startingAtTheEnd.Slot);
        Assert.Equal(new FoundSlot(Press1, bookedUntil, bookedUntil.AddMinutes(CuringLotMinutes)), startingInTheLastMinute.Slot);
    }

    [Theory]
    [InlineData(LotProgressState.Completed, false, RuleCodes.Started)]
    [InlineData(LotProgressState.InProgress, false, RuleCodes.Started)]
    [InlineData(LotProgressState.NotStarted, true, RuleCodes.Cancelled)]
    public void AC_124_a_completed_lot_or_one_of_a_cancelled_order_is_rejected_under_R8(LotProgressState progress, bool orderIsCancelled, string expectedCode)
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotB, CuringLotMinutes, progress: progress, orderIsCancelled: orderIsCancelled);

        EarliestSlotResult result = Find(snapshot, LotB, resourceIds: null, Mon(8));

        Assert.Null(result.Slot);
        RuleViolation violation = Assert.Single(result.Violations);
        Assert.Equal(expectedCode, violation.Code);
        Assert.Equal(new ConflictItem(ConflictKind.Lot, LotB), violation.Conflict);
    }

    [Fact]
    public void AC_116_a_not_before_with_seconds_counts_from_the_next_whole_minute()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotB, CuringLotMinutes);

        EarliestSlotResult result = Find(snapshot, LotB, [Press1], Mon(8).AddSeconds(30));

        Assert.Equal(new FoundSlot(Press1, Mon(8, 1), Mon(12, 1)), result.Slot);
    }

    private static ScheduleSnapshot WithLotAOnPress1() => Empty()
        .WithLot(LotA, CuringLotMinutes)
        .WithLot(LotB, CuringLotMinutes)
        .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12));

    private static EarliestSlotResult Find(ScheduleSnapshot snapshot, int lotId, IReadOnlyList<int>? resourceIds, DateTimeOffset notBefore) =>
        EarliestSlot.Find(snapshot, new EarliestSlotRequest(lotId, resourceIds, notBefore, Horizon));
}

using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// FindConflicts: placements that a later fact made break a rule stay and are listed (DEC-18).
// The stored planned ends are used as they are; the caller recomputes them first (AC-107).
public sealed class ConflictTests
{
    private const int LotA = 101;
    private const int LotB = 102;
    private const int PlacementOfLotA = 201;
    private const int PlacementOfLotB = 202;
    private const int Breakdown = 301;

    [Fact]
    public void A_plant_whose_placements_keep_every_rule_has_no_conflicts()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, 240)
            .WithLot(LotB, 240)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12))
            .WithPlacement(PlacementOfLotB, LotB, Press1, Mon(12), Mon(16));

        Assert.Empty(PlacementRules.FindConflicts(snapshot));
    }

    [Fact]
    public void AC_76_placements_on_a_deactivated_press_are_listed_under_r1()
    {
        ScheduleSnapshot snapshot = TwoLotsOnTwoPresses().WithInactive(Press1);

        PlacementConflict conflict = Assert.Single(PlacementRules.FindConflicts(snapshot));

        Assert.Equal((PlacementOfLotA, LotA), (conflict.PlacementId, conflict.LotId));
        Assert.Equal([RuleCodes.Inactive], conflict.Violations.Select(violation => violation.Code));
    }

    [Fact]
    public void AC_88_a_placement_overlapping_a_later_breakdown_is_listed_under_r3()
    {
        ScheduleSnapshot snapshot = TwoLotsOnTwoPresses().WithUnavailability(Breakdown, Press1, Tue(8), end: null);

        PlacementConflict conflict = Assert.Single(PlacementRules.FindConflicts(snapshot));

        Assert.Equal(PlacementOfLotA, conflict.PlacementId);
        Assert.Equal(new ConflictItem(ConflictKind.Unavailability, Breakdown), Assert.Single(conflict.Violations).Conflict);
    }

    // Lot A's end was recomputed from Monday 04:00 to Tuesday 04:00 after the holiday was added.
    [Fact]
    public void AC_107_a_recomputed_end_that_overlaps_another_placement_lists_both_placements()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithHoliday(NextMonday)
            .WithLot(LotA, 480)
            .WithLot(LotB, 240)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Fri(20), NextTue(4))
            .WithPlacement(PlacementOfLotB, LotB, Press1, NextTue(2), NextTue(6));

        IReadOnlyList<PlacementConflict> conflicts = PlacementRules.FindConflicts(snapshot);

        Assert.Equal([PlacementOfLotA, PlacementOfLotB], conflicts.Select(conflict => conflict.PlacementId));
        Assert.Equal(new ConflictItem(ConflictKind.Placement, PlacementOfLotB), Assert.Single(conflicts[0].Violations).Conflict);
        Assert.Equal(new ConflictItem(ConflictKind.Placement, PlacementOfLotA), Assert.Single(conflicts[1].Violations).Conflict);
    }

    [Fact]
    public void AC_107_a_recomputed_end_after_a_placed_successor_start_lists_both_lots_under_r4()
    {
        const int buildingOperation = 1;
        const int curingOperation = 2;
        ScheduleSnapshot snapshot = Empty()
            .WithHoliday(NextMonday)
            .WithLot(LotA, 480, BuildingType, buildingOperation)
            .WithLot(LotB, 240, CuringType, curingOperation)
            .WithDependency(buildingOperation, curingOperation)
            .WithPlacement(PlacementOfLotA, LotA, Builder1, Fri(20), NextTue(4))
            .WithPlacement(PlacementOfLotB, LotB, Press1, NextTue(2), NextTue(6));

        IReadOnlyList<PlacementConflict> conflicts = PlacementRules.FindConflicts(snapshot);

        Assert.Equal([new ConflictItem(ConflictKind.Lot, LotB)], conflicts.Single(conflict => conflict.LotId == LotA).Violations.Select(violation => violation.Conflict));
        Assert.Equal([new ConflictItem(ConflictKind.Lot, LotA)], conflicts.Single(conflict => conflict.LotId == LotB).Violations.Select(violation => violation.Conflict));
    }

    [Fact]
    public void AC_108_a_placement_whose_start_left_working_time_is_listed_under_r6()
    {
        SnapshotShift[] withoutMondayNight = [.. ThreeShifts(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday)
            .Where(shift => shift is not { Weekday: DayOfWeek.Monday, StartMinuteOfDay: 0 })];
        ScheduleSnapshot snapshot = Empty()
            .WithShifts(withoutMondayNight)
            .WithLot(LotA, 240)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(6), Mon(10));

        PlacementConflict conflict = Assert.Single(PlacementRules.FindConflicts(snapshot));

        Assert.Equal([RuleCodes.OutsideWorkingTime], conflict.Violations.Select(violation => violation.Code));
    }

    [Fact]
    public void A36_a_completed_lot_is_never_listed_because_its_placement_records_where_it_ran()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, 240, progress: LotProgressState.Completed)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12))
            .WithInactive(Press1)
            .WithUnavailability(Breakdown, Press1, Mon(9), end: null);

        Assert.Empty(PlacementRules.FindConflicts(snapshot));
    }

    [Fact]
    public void A36_an_in_progress_lot_hit_by_a_breakdown_is_listed()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, 240, progress: LotProgressState.InProgress)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12))
            .WithUnavailability(Breakdown, Press1, Mon(9), end: null);

        Assert.Equal(PlacementOfLotA, Assert.Single(PlacementRules.FindConflicts(snapshot)).PlacementId);
    }

    private static ScheduleSnapshot TwoLotsOnTwoPresses() => Empty()
        .WithLot(LotA, 240)
        .WithLot(LotB, 240)
        .WithPlacement(PlacementOfLotA, LotA, Press1, Tue(6), Tue(10))
        .WithPlacement(PlacementOfLotB, LotB, Press2, Tue(6), Tue(10));
}

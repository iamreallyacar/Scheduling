using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// R7 (every violation reported, nothing adjusted) and R8 (started, completed and cancelled lots).
public sealed class ViolationTests
{
    private const int LotA = 101;
    private const int LotB = 102;
    private const int BuildingLot = 103;
    private const int PlacementOfLotA = 201;

    [Fact]
    public void AC_110_a_placement_breaking_r2_and_r4_reports_both_with_r2_first()
    {
        const int buildingOperation = 1;
        const int curingOperation = 2;
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, 240)
            .WithLot(BuildingLot, 240, BuildingType, buildingOperation)
            .WithLot(LotB, 240, CuringType, curingOperation)
            .WithDependency(buildingOperation, curingOperation)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12))
            .WithPlacement(202, BuildingLot, Builder1, Mon(8), Mon(12));

        PlacementCheck check = snapshot.Place(LotB, Press1, Mon(11));

        Assert.Equal(
            [new ConflictItem(ConflictKind.Placement, PlacementOfLotA), new ConflictItem(ConflictKind.Lot, BuildingLot)],
            check.Violations.Select(violation => violation.Conflict));
        Assert.Equal(["R2", "R4"], check.Violations.Select(violation => violation.Rule));
    }

    [Fact]
    public void AC_111_a_rejected_lot_is_not_moved_its_planned_end_follows_the_requested_start()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotA, 240).WithLot(LotB, 240).WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12));

        PlacementCheck check = snapshot.Place(LotB, Press1, Mon(11));

        Assert.False(check.IsAccepted);
        Assert.Equal(Mon(15), check.PlannedEnd);
    }

    [Fact]
    public void AC_112_a_completed_lot_cannot_be_placed_moved_or_removed()
    {
        ScheduleSnapshot unplaced = Empty().WithLot(LotA, 240, progress: LotProgressState.Completed);
        ScheduleSnapshot placed = unplaced.WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12));

        RuleAssert.RejectedFirst(unplaced.Place(LotA, Press1, Mon(8)), RuleCodes.Started, ConflictKind.Lot, LotA);
        RuleAssert.RejectedFirst(placed.Place(LotA, Press1, Mon(14)), RuleCodes.Started, ConflictKind.Lot, LotA);
        Assert.Equal(RuleCodes.Started, Assert.Single(PlacementRules.ValidateRemoval(placed, LotA)).Code);
    }

    [Fact]
    public void AC_113_an_in_progress_lot_cannot_be_moved_or_removed()
    {
        ScheduleSnapshot placed = Empty()
            .WithLot(LotA, 240, progress: LotProgressState.InProgress)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12));

        RuleViolation violation = RuleAssert.RejectedFirst(placed.Place(LotA, Press2, Mon(8)), RuleCodes.Started, ConflictKind.Lot, LotA);
        Assert.Equal("R8", violation.Rule);
        Assert.Equal(new ConflictItem(ConflictKind.Lot, LotA), Assert.Single(PlacementRules.ValidateRemoval(placed, LotA)).Conflict);
    }

    [Fact]
    public void A_lot_that_has_not_started_can_be_removed()
    {
        ScheduleSnapshot placed = Empty().WithLot(LotA, 240).WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12));

        Assert.Empty(PlacementRules.ValidateRemoval(placed, LotA));
    }

    [Fact]
    public void AC_114_a_lot_of_a_cancelled_order_cannot_be_placed()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotA, 240, orderIsCancelled: true);

        RuleAssert.RejectedFirst(snapshot.Place(LotA, Press1, Mon(8)), RuleCodes.Cancelled, ConflictKind.Lot, LotA);
    }

    [Fact]
    public void A35_a_start_outside_working_time_still_reports_the_rules_that_need_no_planned_end()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotA, 240, orderIsCancelled: true);

        PlacementCheck check = snapshot.Place(LotA, Builder1, Sat(10));

        Assert.Null(check.PlannedEnd);
        Assert.Equal([RuleCodes.ResourceType, RuleCodes.OutsideWorkingTime, RuleCodes.Cancelled], check.Violations.Select(violation => violation.Code));
    }

    [Fact]
    public void A_lot_or_resource_missing_from_the_snapshot_is_an_error()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(LotA, 240);

        Assert.Throws<ArgumentException>(() => snapshot.Place(LotB, Press1, Mon(8)));
        Assert.Throws<ArgumentException>(() => snapshot.Place(LotA, 999, Mon(8)));
    }
}

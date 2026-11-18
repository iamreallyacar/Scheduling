using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// R1 (type and active resource), R2 (no overlap) and R3 (no overlap with unavailability).
public sealed class ResourceRuleTests
{
    private const int LotA = 101;
    private const int LotB = 102;
    private const int PlacementOfLotA = 201;
    private const int Maintenance = 301;
    private const int Breakdown = 302;

    [Fact]
    public void AC_73_a_curing_lot_on_a_free_press_at_working_time_is_accepted()
    {
        PlacementCheck check = Empty().WithLot(LotA, 240).Place(LotA, Press1, Mon(8));

        RuleAssert.Accepted(check);
        Assert.Equal(Mon(12), check.PlannedEnd);
    }

    [Fact]
    public void AC_74_a_curing_lot_on_a_builder_is_rejected_with_the_resource_as_conflict()
    {
        PlacementCheck check = Empty().WithLot(LotA, 240).Place(LotA, Builder1, Mon(8));

        RuleViolation violation = RuleAssert.RejectedFirst(check, RuleCodes.ResourceType, ConflictKind.Resource, Builder1);
        Assert.Equal("R1", violation.Rule);
    }

    [Fact]
    public void AC_75_a_lot_on_an_inactive_press_is_rejected_with_the_resource_as_conflict()
    {
        PlacementCheck check = Empty().WithInactive(Press1).WithLot(LotA, 240).Place(LotA, Press1, Mon(8));

        RuleAssert.RejectedFirst(check, RuleCodes.Inactive, ConflictKind.Resource, Press1);
    }

    [Fact]
    public void AC_78_a_lot_overlapping_a_placement_on_the_same_press_is_rejected_with_that_placement_as_conflict()
    {
        PlacementCheck check = WithLotAOnPress1().WithLot(LotB, 240).Place(LotB, Press1, Mon(11));

        RuleViolation violation = RuleAssert.RejectedFirst(check, RuleCodes.Overlap, ConflictKind.Placement, PlacementOfLotA);
        Assert.Equal("R2", violation.Rule);
    }

    [Fact]
    public void AC_79_a_lot_starting_when_the_other_ends_is_accepted()
    {
        RuleAssert.Accepted(WithLotAOnPress1().WithLot(LotB, 240).Place(LotB, Press1, Mon(12)));
    }

    // Lot B ending exactly when Lot A starts touches it from the other side.
    [Fact]
    public void AC_79_a_lot_ending_when_the_other_starts_is_accepted()
    {
        RuleAssert.Accepted(WithLotAOnPress1().WithLot(LotB, 120).Place(LotB, Press1, Mon(6)));
    }

    [Fact]
    public void AC_80_the_same_time_on_another_press_is_accepted()
    {
        RuleAssert.Accepted(WithLotAOnPress1().WithLot(LotB, 240).Place(LotB, Press2, Mon(11)));
    }

    [Fact]
    public void AC_81_a_lot_never_conflicts_with_its_own_current_placement()
    {
        RuleAssert.Accepted(WithLotAOnPress1().Place(LotA, Press1, Mon(10)));
    }

    [Fact]
    public void AC_84_a_lot_running_into_planned_maintenance_is_rejected_with_the_maintenance_as_conflict()
    {
        ScheduleSnapshot snapshot = Empty().WithUnavailability(Maintenance, Press1, Tue(6), Tue(10)).WithLot(LotA, 240);

        RuleViolation violation = RuleAssert.RejectedFirst(snapshot.Place(LotA, Press1, Tue(4)), RuleCodes.Unavailable, ConflictKind.Unavailability, Maintenance);
        Assert.Equal("R3", violation.Rule);
    }

    [Fact]
    public void AC_85_a_breakdown_without_an_end_blocks_everything_after_its_start()
    {
        ScheduleSnapshot snapshot = Empty().WithUnavailability(Breakdown, Press1, Wed(9), end: null).WithLot(LotA, 240);

        RuleAssert.RejectedFirst(snapshot.Place(LotA, Press1, NextMon(8)), RuleCodes.Unavailable, ConflictKind.Unavailability, Breakdown);
    }

    [Fact]
    public void AC_85_once_the_breakdown_ends_before_that_monday_the_lot_is_accepted()
    {
        ScheduleSnapshot snapshot = Empty().WithUnavailability(Breakdown, Press1, Wed(9), Fri(12)).WithLot(LotA, 240);

        RuleAssert.Accepted(snapshot.Place(LotA, Press1, NextMon(8)));
    }

    [Fact]
    public void AC_86_a_lot_ending_when_an_open_breakdown_starts_is_accepted()
    {
        ScheduleSnapshot snapshot = Empty().WithUnavailability(Breakdown, Press1, Wed(9), end: null).WithLot(LotA, 240);

        PlacementCheck check = snapshot.Place(LotA, Press1, Wed(5));

        RuleAssert.Accepted(check);
        Assert.Equal(Wed(9), check.PlannedEnd);
    }

    [Fact]
    public void AC_87_maintenance_during_the_weekend_pause_of_a_lot_is_an_overlap()
    {
        ScheduleSnapshot snapshot = Empty().WithUnavailability(Maintenance, Press1, Sat(10), Sat(12)).WithLot(LotA, 480);

        RuleAssert.RejectedFirst(snapshot.Place(LotA, Press1, Fri(20)), RuleCodes.Unavailable, ConflictKind.Unavailability, Maintenance);
    }

    [Fact]
    public void An_unavailability_of_another_press_does_not_block()
    {
        ScheduleSnapshot snapshot = Empty().WithUnavailability(Maintenance, Press2, Tue(6), Tue(10)).WithLot(LotA, 240);

        RuleAssert.Accepted(snapshot.Place(LotA, Press1, Tue(4)));
    }

    private static ScheduleSnapshot WithLotAOnPress1() =>
        Empty().WithLot(LotA, 240).WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12));
}

using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// R4: every placed successor lot starts no earlier than every placed predecessor lot's planned end
// plus the lag, in either placement order. Stage 1 is building on Builder 1, stage 2 curing.
public sealed class DependencyRuleTests
{
    private const int Stage1 = 1;
    private const int Stage2 = 2;
    private const int Stage3 = 3;
    private const int Stage1Lot = 101;
    private const int Stage2Lot = 102;
    private const int SecondStage1Lot = 103;
    private const int Stage3Lot = 104;

    [Fact]
    public void AC_89_a_successor_starting_before_its_predecessor_ends_is_rejected_with_the_predecessor_as_conflict()
    {
        PlacementCheck check = Stage1EndsMonday10(lagMinutes: 0).Place(Stage2Lot, Press1, Mon(9));

        RuleViolation violation = RuleAssert.RejectedFirst(check, RuleCodes.Dependency, ConflictKind.Lot, Stage1Lot);
        Assert.Equal("R4", violation.Rule);
    }

    [Fact]
    public void AC_90_a_successor_starting_when_its_predecessor_ends_is_accepted()
    {
        RuleAssert.Accepted(Stage1EndsMonday10(lagMinutes: 0).Place(Stage2Lot, Press1, Mon(10)));
    }

    [Fact]
    public void AC_91_a_start_inside_the_lag_is_rejected_and_names_the_earliest_allowed_start()
    {
        PlacementCheck check = Stage1EndsMonday10(lagMinutes: 120).Place(Stage2Lot, Press1, Mon(11));

        RuleViolation violation = RuleAssert.RejectedFirst(check, RuleCodes.Dependency, ConflictKind.Lot, Stage1Lot);
        Assert.Equal(Mon(12), violation.EarliestAllowedStart);
    }

    [Fact]
    public void AC_92_lag_is_elapsed_time_and_runs_over_the_weekend()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(Stage1Lot, 240, BuildingType, Stage1)
            .WithLot(Stage2Lot, 240, CuringType, Stage2)
            .WithDependency(Stage1, Stage2, 120)
            .WithPlacement(201, Stage1Lot, Builder1, Fri(19), Fri(23));

        RuleAssert.Accepted(snapshot.Place(Stage2Lot, Press1, NextMon(0)));
    }

    [Fact]
    public void AC_93_a_predecessor_placed_after_its_successor_must_end_the_lag_before_it()
    {
        PlacementCheck check = CuringPlacedWednesday08().Place(Stage1Lot, Builder1, Wed(2));

        RuleAssert.RejectedFirst(check, RuleCodes.Dependency, ConflictKind.Lot, Stage2Lot);
    }

    [Fact]
    public void AC_94_a_predecessor_ending_exactly_the_lag_before_its_successor_is_accepted()
    {
        RuleAssert.Accepted(CuringPlacedWednesday08().Place(Stage1Lot, Builder1, Wed(0)));
    }

    // A11: every successor lot waits for every predecessor lot, so the later one is the conflict.
    [Fact]
    public void AC_95_with_two_predecessor_lots_the_one_ending_later_is_the_conflict()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(Stage1Lot, 240, BuildingType, Stage1)
            .WithLot(SecondStage1Lot, 240, BuildingType, Stage1)
            .WithLot(Stage2Lot, 240, CuringType, Stage2)
            .WithDependency(Stage1, Stage2)
            .WithPlacement(201, Stage1Lot, Builder1, Mon(6), Mon(10))
            .WithPlacement(202, SecondStage1Lot, Builder1, Mon(10), Mon(14));

        PlacementCheck check = snapshot.Place(Stage2Lot, Press1, Mon(12));

        RuleAssert.RejectedFirst(check, RuleCodes.Dependency, ConflictKind.Lot, SecondStage1Lot);
        Assert.Single(check.Violations);
    }

    [Fact]
    public void AC_96_with_parallel_branches_the_later_branch_is_the_conflict()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(Stage1Lot, 240, BuildingType, Stage1)
            .WithLot(Stage2Lot, 240, CuringType, Stage2)
            .WithLot(Stage3Lot, 240, CuringType, Stage3)
            .WithDependency(Stage1, Stage3)
            .WithDependency(Stage2, Stage3)
            .WithPlacement(201, Stage1Lot, Builder1, Mon(6), Mon(10))
            .WithPlacement(202, Stage2Lot, Press2, Mon(8), Mon(12));

        RuleAssert.RejectedFirst(snapshot.Place(Stage3Lot, Press1, Mon(11)), RuleCodes.Dependency, ConflictKind.Lot, Stage2Lot);
    }

    [Fact]
    public void AC_97_an_unplaced_predecessor_imposes_nothing()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(Stage1Lot, 240, BuildingType, Stage1)
            .WithLot(Stage2Lot, 240, CuringType, Stage2)
            .WithDependency(Stage1, Stage2, 120);

        RuleAssert.Accepted(snapshot.Place(Stage2Lot, Press1, Mon(0)));
    }

    [Fact]
    public void AC_98_moving_a_predecessor_past_its_successor_start_is_rejected_with_the_successor_as_conflict()
    {
        ScheduleSnapshot snapshot = Stage1EndsMonday10(lagMinutes: 0).WithPlacement(202, Stage2Lot, Press1, Mon(10), Mon(14));

        RuleAssert.RejectedFirst(snapshot.Place(Stage1Lot, Builder1, Mon(8)), RuleCodes.Dependency, ConflictKind.Lot, Stage2Lot);
    }

    [Fact]
    public void AC_99_removing_a_placement_never_breaks_a_dependency()
    {
        ScheduleSnapshot snapshot = Stage1EndsMonday10(lagMinutes: 0).WithPlacement(202, Stage2Lot, Press1, Mon(10), Mon(14));

        Assert.Empty(PlacementRules.ValidateRemoval(snapshot, Stage1Lot));
        Assert.Empty(PlacementRules.ValidateRemoval(snapshot, Stage2Lot));
    }

    private static ScheduleSnapshot Stage1EndsMonday10(int lagMinutes) => Empty()
        .WithLot(Stage1Lot, 240, BuildingType, Stage1)
        .WithLot(Stage2Lot, 240, CuringType, Stage2)
        .WithDependency(Stage1, Stage2, lagMinutes)
        .WithPlacement(201, Stage1Lot, Builder1, Mon(6), Mon(10));

    private static ScheduleSnapshot CuringPlacedWednesday08() => Empty()
        .WithLot(Stage1Lot, 360, BuildingType, Stage1)
        .WithLot(Stage2Lot, 240, CuringType, Stage2)
        .WithDependency(Stage1, Stage2, 120)
        .WithPlacement(202, Stage2Lot, Press1, Wed(8), Wed(12));
}

using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// One context checks many requests, as the earliest-slot search does per candidate (DEC-47).
public sealed class PlacementContextTests
{
    private const int LotA = 101;
    private const int LotB = 102;
    private const int LotC = 103;
    private const int PlacementOfLotA = 201;
    private const int MorningCalendarId = 2;
    private const int MorningPress = 14;

    [Fact]
    public void AC_78_one_context_checks_each_request_against_the_snapshot_and_never_counts_an_earlier_request_as_placed()
    {
        PlacementContext context = new(Empty()
            .WithLot(LotA, 240)
            .WithLot(LotB, 240)
            .WithLot(LotC, 240)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(8), Mon(12)));

        PlacementCheck overlapping = context.Validate(new PlacementRequest(LotB, Press1, Mon(11)));
        PlacementCheck afterLotA = context.Validate(new PlacementRequest(LotB, Press1, Mon(12)));
        PlacementCheck sameSlotForAnotherLot = context.Validate(new PlacementRequest(LotC, Press1, Mon(12)));
        PlacementCheck overlappingAgain = context.Validate(new PlacementRequest(LotB, Press1, Mon(11)));

        RuleAssert.RejectedFirst(overlapping, RuleCodes.Overlap, ConflictKind.Placement, PlacementOfLotA);
        RuleAssert.Accepted(afterLotA);
        RuleAssert.Accepted(sameSlotForAnotherLot);
        RuleAssert.RejectedFirst(overlappingAgain, RuleCodes.Overlap, ConflictKind.Placement, PlacementOfLotA);
    }

    [Fact]
    public void AC_104_one_context_gives_each_resource_the_end_from_its_own_calendar()
    {
        ScheduleSnapshot plant = Empty();
        SnapshotShift[] morningShifts = [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(weekday => new SnapshotShift(weekday, 360, 840))];
        PlacementContext context = new(plant with
        {
            Calendars = [.. plant.Calendars, new SnapshotCalendar(MorningCalendarId, morningShifts)],
            Resources = [.. plant.Resources, new SnapshotResource(MorningPress, CuringType, MorningCalendarId, IsActive: true)],
        });

        Assert.Equal(Tue(12), context.EndAfter(MorningPress, Mon(10), 600));
        Assert.Equal(Mon(20), context.EndAfter(Press1, Mon(10), 600));
    }
}

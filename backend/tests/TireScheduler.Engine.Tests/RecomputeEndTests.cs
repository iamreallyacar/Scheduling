using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// RecomputeEnds: the stored planned ends after a shift or holiday change (AC-182, A42). Lot A
// takes 8 hours from Friday 20:00: 4 hours on Friday, then 4 more after the weekend.
public sealed class RecomputeEndTests
{
    private const int LotA = 101;
    private const int LotB = 102;
    private const int PlacementOfLotA = 201;
    private const int PlacementOfLotB = 202;
    private const int EightHours = 480;

    [Fact]
    public void AC_182_AC_107_a_new_holiday_on_the_monday_moves_the_end_to_tuesday()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithHoliday(NextMonday)
            .WithLot(LotA, EightHours)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Fri(20), NextMon(4));

        Assert.Equal([new RecomputedEnd(PlacementOfLotA, NextTue(4))], PlacementRules.RecomputeEnds(snapshot));
    }

    [Fact]
    public void AC_182_a_deleted_holiday_moves_the_end_earlier()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(LotA, EightHours)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Fri(20), NextTue(4));

        Assert.Equal([new RecomputedEnd(PlacementOfLotA, NextMon(4))], PlacementRules.RecomputeEnds(snapshot));
    }

    [Fact]
    public void AC_182_a_placement_whose_end_stays_the_same_is_not_listed()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithHoliday(NextMonday)
            .WithLot(LotA, EightHours)
            .WithLot(LotB, 240)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Fri(20), NextMon(4))
            .WithPlacement(PlacementOfLotB, LotB, Press2, Mon(8), Mon(12));

        Assert.Equal([PlacementOfLotA], PlacementRules.RecomputeEnds(snapshot).Select(end => end.PlacementId));
    }

    [Fact]
    public void A42_a_placement_whose_start_left_working_time_keeps_its_stored_end()
    {
        SnapshotShift[] withoutMondayNight = [.. ThreeShifts(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday)
            .Where(shift => shift is not { Weekday: DayOfWeek.Monday, StartMinuteOfDay: 0 })];
        ScheduleSnapshot snapshot = Empty()
            .WithShifts(withoutMondayNight)
            .WithLot(LotA, 240)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Mon(6), Mon(10));

        Assert.Empty(PlacementRules.RecomputeEnds(snapshot));
    }

    [Fact]
    public void A42_a_completed_lot_keeps_its_end_and_an_in_progress_lot_is_recomputed()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithHoliday(NextMonday)
            .WithLot(LotA, EightHours, progress: LotProgressState.Completed)
            .WithLot(LotB, EightHours, progress: LotProgressState.InProgress)
            .WithPlacement(PlacementOfLotA, LotA, Press1, Fri(20), NextMon(4))
            .WithPlacement(PlacementOfLotB, LotB, Press2, Fri(20), NextMon(4));

        Assert.Equal([new RecomputedEnd(PlacementOfLotB, NextTue(4))], PlacementRules.RecomputeEnds(snapshot));
    }
}

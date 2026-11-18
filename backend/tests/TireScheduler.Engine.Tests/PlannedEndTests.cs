using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// R6: the planned end consumes working time from the resource's calendar (domain-model.md section 4).
public sealed class PlannedEndTests
{
    private const int LotA = 101;

    [Fact]
    public void AC_100_an_8_hour_lot_starting_friday_20_00_ends_monday_04_00()
    {
        Assert.Equal(NextMon(4), PlacementRules.ComputeEnd(Empty(), Press1, Fri(20), 480));
    }

    [Fact]
    public void AC_101_with_a_holiday_on_monday_an_8_hour_lot_starting_friday_20_00_ends_tuesday_04_00()
    {
        ScheduleSnapshot snapshot = Empty().WithHoliday(NextMonday).WithLot(LotA, 480);

        PlacementCheck check = snapshot.Place(LotA, Press1, Fri(20));

        RuleAssert.Accepted(check);
        Assert.Equal(NextTue(4), check.PlannedEnd);
    }

    public static TheoryData<string> StartsOutsideWorkingTime => ["saturday 10:00", "friday 24:00", "a holiday"];

    [Theory]
    [MemberData(nameof(StartsOutsideWorkingTime))]
    public void AC_102_a_start_outside_working_time_is_rejected(string startCase)
    {
        ScheduleSnapshot snapshot = Empty().WithHoliday(NextMonday).WithLot(LotA, 60);
        DateTimeOffset start = startCase switch
        {
            "saturday 10:00" => Sat(10),
            "friday 24:00" => Sat(0),
            _ => NextMon(8),
        };

        PlacementCheck check = snapshot.Place(LotA, Press1, start);

        RuleAssert.RejectedFirst(check, RuleCodes.OutsideWorkingTime, ConflictKind.Resource, Press1);
        Assert.Null(PlacementRules.ComputeEnd(snapshot, Press1, start, 60));
    }

    [Fact]
    public void AC_103_an_end_that_falls_on_a_shift_end_stays_there()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(MorningShifts());

        Assert.Equal(Mon(14), PlacementRules.ComputeEnd(snapshot, Press1, Mon(10), 240));
    }

    [Fact]
    public void AC_104_a_lot_longer_than_the_rest_of_the_shift_continues_in_the_next_shift()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(MorningShifts());

        Assert.Equal(Tue(12), PlacementRules.ComputeEnd(snapshot, Press1, Mon(10), 600));
    }

    // 2027-03-28 00:00 in Berlin is 2027-03-27 23:00Z; the clocks then go from 02:00 to 03:00.
    [Fact]
    public void AC_105_a_lot_across_spring_forward_works_elapsed_time_and_skips_the_missing_hour()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(AllDayEveryDay());

        Assert.Equal(Utc(2027, 3, 28, 3, 0), PlacementRules.ComputeEnd(snapshot, Press1, Utc(2027, 3, 27, 23, 0), 240));
    }

    // 2026-10-25 00:00 in Berlin is 2026-10-24 22:00Z; the hour from 02:00 to 03:00 happens twice.
    [Fact]
    public void AC_106_a_lot_across_fall_back_works_elapsed_time_and_counts_the_repeated_hour_twice()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(AllDayEveryDay());

        Assert.Equal(Utc(2026, 10, 25, 2, 0), PlacementRules.ComputeEnd(snapshot, Press1, Utc(2026, 10, 24, 22, 0), 240));
    }

    [Fact]
    public void AC_106_the_three_shift_pattern_gives_25_working_hours_on_the_fall_back_day()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(AllDayEveryDay());

        DateTimeOffset? end = PlacementRules.ComputeEnd(snapshot, Press1, Utc(2026, 10, 24, 22, 0), 25 * 60);

        Assert.Equal(Utc(2026, 10, 25, 23, 0), end);
    }

    // A local time that occurs twice belongs to both occurrences, so a shift starting at 02:30 on
    // the fall-back day begins at the first 02:30 (00:30Z).
    [Fact]
    public void AC_106_a_shift_starting_in_the_repeated_hour_starts_at_its_first_occurrence()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(new SnapshotShift(DayOfWeek.Sunday, 150, 480));

        Assert.Equal(Utc(2026, 10, 25, 1, 30), PlacementRules.ComputeEnd(snapshot, Press1, Utc(2026, 10, 25, 0, 30), 60));
    }

    // A shift ending at 02:30 on the fall-back day ends at the second 02:30 (01:30Z), so it holds
    // three and a half hours from 00:00 (22:00Z).
    [Fact]
    public void AC_106_a_shift_ending_in_the_repeated_hour_ends_at_its_last_occurrence()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(new SnapshotShift(DayOfWeek.Sunday, 0, 150));

        Assert.Equal(Utc(2026, 10, 25, 1, 30), PlacementRules.ComputeEnd(snapshot, Press1, Utc(2026, 10, 24, 22, 0), 210));
    }

    // 01:00Z is the second 02:00 of the fall-back day.
    [Fact]
    public void AC_106_a_start_in_the_second_occurrence_of_the_repeated_hour_is_working_time()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(new SnapshotShift(DayOfWeek.Sunday, 0, 150)).WithLot(LotA, 30);

        PlacementCheck check = snapshot.Place(LotA, Press1, Utc(2026, 10, 25, 1, 0));

        RuleAssert.Accepted(check);
        Assert.Equal(Utc(2026, 10, 25, 1, 30), check.PlannedEnd);
    }

    // On 2027-03-28 local 02:30 does not exist; the shift ends where the gap ends, 03:00 local (01:00Z).
    [Fact]
    public void AC_105_a_shift_ending_inside_the_spring_forward_gap_ends_when_the_gap_ends()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(new SnapshotShift(DayOfWeek.Sunday, 0, 150));

        Assert.Equal(Utc(2027, 3, 28, 1, 0), PlacementRules.ComputeEnd(snapshot, Press1, Utc(2027, 3, 27, 23, 0), 120));
    }

    [Fact]
    public void AC_105_a_shift_lying_wholly_inside_the_spring_forward_gap_has_no_working_time()
    {
        ScheduleSnapshot snapshot = Empty().WithShifts(new SnapshotShift(DayOfWeek.Sunday, 120, 180));

        Assert.Null(PlacementRules.ComputeEnd(snapshot, Press1, Utc(2027, 3, 28, 1, 0), 60));
    }

    private static SnapshotShift[] MorningShifts() =>
        [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(weekday => new SnapshotShift(weekday, 360, 840))];

    private static SnapshotShift[] AllDayEveryDay() => ThreeShifts(Enum.GetValues<DayOfWeek>());
}

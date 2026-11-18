using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// The working periods the timeline shades around (AC-186, DEC-45): the same periods as R6 reads,
// merged where they touch or overlap and cut at the range.
public sealed class WorkingPeriodTests
{
    private static readonly SnapshotCalendar PlantDefault = Empty().Calendars[0];

    private static readonly SnapshotCalendar EveryDay = new(PlantCalendarId, ThreeShifts(Enum.GetValues<DayOfWeek>()));

    [Fact]
    public void AC_186_the_shifts_of_a_day_merge_into_one_period_and_weekends_and_holidays_give_none()
    {
        HashSet<DateOnly> holidays = [new DateOnly(2026, 11, 18)];

        IReadOnlyList<WorkingPeriod> periods = WorkingPeriods.Between(PlantDefault, holidays, Berlin, Mon(0), NextMon(0));

        Assert.Equal([new WorkingPeriod(Mon(0), Wed(0)), new WorkingPeriod(Local(new DateOnly(2026, 11, 19), 0), Sat(0))], periods);
    }

    [Fact]
    public void AC_186_periods_are_cut_at_the_range()
    {
        IReadOnlyList<WorkingPeriod> periods = WorkingPeriods.Between(PlantDefault, new HashSet<DateOnly>(), Berlin, Tue(6), Tue(12));

        Assert.Equal([new WorkingPeriod(Tue(6), Tue(12))], periods);
    }

    [Fact]
    public void AC_186_shifts_with_a_gap_between_them_stay_separate_periods()
    {
        SnapshotCalendar twoShifts = new(PlantCalendarId, [new SnapshotShift(DayOfWeek.Monday, 360, 840), new SnapshotShift(DayOfWeek.Monday, 900, 1320)]);

        IReadOnlyList<WorkingPeriod> periods = WorkingPeriods.Between(twoShifts, new HashSet<DateOnly>(), Berlin, Mon(0), Tue(0));

        Assert.Equal([new WorkingPeriod(Mon(6), Mon(14)), new WorkingPeriod(Mon(15), Mon(22))], periods);
    }

    // Clocks go back on Sun 2026-10-25 at 03:00 to 02:00, and forward on Sun 2026-03-29 at 02:00 to 03:00.
    [Fact]
    public void AC_186_a_working_day_is_25_hours_on_a_fall_back_day_and_23_hours_on_a_spring_forward_day()
    {
        WorkingPeriod fallBack = Assert.Single(WorkingPeriods.Between(EveryDay, new HashSet<DateOnly>(), Berlin, Utc(2026, 10, 24, 22, 0), Utc(2026, 10, 25, 23, 0)));
        WorkingPeriod springForward = Assert.Single(WorkingPeriods.Between(EveryDay, new HashSet<DateOnly>(), Berlin, Utc(2026, 3, 28, 23, 0), Utc(2026, 3, 29, 22, 0)));

        Assert.Equal(TimeSpan.FromHours(25), fallBack.End - fallBack.Start);
        Assert.Equal(TimeSpan.FromHours(23), springForward.End - springForward.Start);
    }

    // A shift that ends at 02:30 on the fall-back day runs to the second 02:30, after the next shift starts at the first.
    [Fact]
    public void AC_186_shifts_that_overlap_across_a_repeated_local_hour_merge_into_one_period()
    {
        SnapshotCalendar splitAtRepeatedHour = new(PlantCalendarId, [new SnapshotShift(DayOfWeek.Sunday, 0, 150), new SnapshotShift(DayOfWeek.Sunday, 150, 480)]);

        IReadOnlyList<WorkingPeriod> periods = WorkingPeriods.Between(splitAtRepeatedHour, new HashSet<DateOnly>(), Berlin, Utc(2026, 10, 24, 22, 0), Utc(2026, 10, 25, 23, 0));

        Assert.Equal([new WorkingPeriod(Utc(2026, 10, 24, 22, 0), Utc(2026, 10, 25, 7, 0))], periods);
    }
}

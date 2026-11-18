namespace TireScheduler.Engine;

// The working time of a calendar over a range, for views that show it (DEC-45). The periods are
// the ones R6 reads (engine README, "Working time").
public static class WorkingPeriods
{
    public static IReadOnlyList<WorkingPeriod> Between(
        SnapshotCalendar calendar,
        IReadOnlySet<DateOnly> holidays,
        string timeZoneId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return new WorkingTime(calendar, holidays, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).PeriodsBetween(rangeStart, rangeEnd);
    }
}

// Half-open: the end is not working time.
public sealed record WorkingPeriod(DateTimeOffset Start, DateTimeOffset End);

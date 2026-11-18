namespace TireScheduler.Engine;

// The working time of one calendar as UTC periods (R6, domain-model.md section 4). Shifts are
// plant-local, so on a DST day a period is an hour shorter or longer in elapsed time.
internal sealed class WorkingTime
{
    private readonly TimeZoneInfo zone;
    private readonly IReadOnlySet<DateOnly> holidays;
    private readonly ILookup<DayOfWeek, SnapshotShift> shiftsByWeekday;

    public WorkingTime(SnapshotCalendar calendar, IReadOnlySet<DateOnly> holidays, TimeZoneInfo zone)
    {
        this.zone = zone;
        this.holidays = holidays;
        shiftsByWeekday = calendar.Shifts.ToLookup(shift => shift.Weekday);
    }

    // Periods are half-open, so the end of the last shift of a day is not working time (AC-102).
    public bool Contains(DateTimeOffset instant)
    {
        DateOnly localDate = PlantTime.LocalDate(instant, zone);

        // Periods of the neighbouring days can reach across local midnight when it occurs twice.
        return Enumerable.Range(-1, 3)
            .SelectMany(dayOffset => PeriodsOn(localDate.AddDays(dayOffset)))
            .Any(period => period.Start <= instant && instant < period.End);
    }

    // Null when the start is outside working time. Ends as soon as the duration is used up, so an
    // end on a shift end stays there (AC-103).
    public DateTimeOffset? EndAfter(DateTimeOffset start, int durationMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);
        if (!Contains(start))
        {
            return null;
        }

        // Ends: the start is working time, so the calendar has shifts, and the holidays are finite.
        TimeSpan remaining = TimeSpan.FromMinutes(durationMinutes);
        DateTimeOffset workedUntil = start;
        for (DateOnly date = PlantTime.LocalDate(start, zone).AddDays(-1); ; date = date.AddDays(1))
        {
            foreach ((DateTimeOffset periodStart, DateTimeOffset periodEnd) in PeriodsOn(date))
            {
                // Periods can overlap where a local time occurs twice; each instant counts once.
                DateTimeOffset from = periodStart > workedUntil ? periodStart : workedUntil;
                if (periodEnd <= from)
                {
                    continue;
                }

                if (remaining <= periodEnd - from)
                {
                    return from + remaining;
                }

                remaining -= periodEnd - from;
                workedUntil = periodEnd;
            }
        }
    }

    // Ordered, merged where periods touch or overlap, and cut at the range (DEC-45).
    public IReadOnlyList<WorkingPeriod> PeriodsBetween(DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        // Periods of the neighbouring days can reach across local midnight when it occurs twice.
        DateOnly firstDate = PlantTime.LocalDate(rangeStart, zone).AddDays(-1);
        int dayCount = PlantTime.LocalDate(rangeEnd, zone).AddDays(1).DayNumber - firstDate.DayNumber + 1;
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> periods = Enumerable.Range(0, dayCount)
            .SelectMany(dayOffset => PeriodsOn(firstDate.AddDays(dayOffset)))
            .Where(period => period.Start < rangeEnd && rangeStart < period.End)
            .OrderBy(period => period.Start);
        List<WorkingPeriod> merged = [];
        foreach ((DateTimeOffset start, DateTimeOffset end) in periods)
        {
            DateTimeOffset cutStart = start > rangeStart ? start : rangeStart;
            DateTimeOffset cutEnd = end < rangeEnd ? end : rangeEnd;
            if (merged.Count > 0 && cutStart <= merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = cutEnd > merged[^1].End ? cutEnd : merged[^1].End };
            }
            else
            {
                merged.Add(new WorkingPeriod(cutStart, cutEnd));
            }
        }

        return merged;
    }

    private IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> PeriodsOn(DateOnly date)
    {
        if (holidays.Contains(date))
        {
            return [];
        }

        DateTime midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return shiftsByWeekday[date.DayOfWeek]
            .Select(shift => (
                Start: PlantTime.EarliestInstant(midnight.AddMinutes(shift.StartMinuteOfDay), zone),
                End: PlantTime.LatestInstant(midnight.AddMinutes(shift.EndMinuteOfDay), zone)))
            .Where(period => period.Start < period.End)
            .OrderBy(period => period.Start);
    }
}

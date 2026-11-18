namespace TireScheduler.Engine;

// What the late flag of one order needs, for one schedule version (domain-model.md section 6).
// PlannedEnds are the planned ends of the order's lots in that version. CompletedAt is the last
// actual end once every lot is completed, and null before.
public sealed record LatenessFacts(
    DateOnly DueDate,
    string PlantTimeZoneId,
    DateTimeOffset Now,
    bool IsCancelled,
    IReadOnlyCollection<DateTimeOffset> PlannedEnds,
    DateTimeOffset? CompletedAt);

// The late flag (DEC-1, DEC-30). A cancelled order is never late; a completed one is late when it
// finished after its due day; an unfinished one when that day is over or a lot is planned to end
// after it.
public static class OrderLateness
{
    public static bool IsLate(LatenessFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.IsCancelled)
        {
            return false;
        }

        DateTimeOffset endOfDueDay = PlantTime.EndOfDay(facts.DueDate, facts.PlantTimeZoneId);
        if (facts.CompletedAt is { } completedAt)
        {
            return completedAt > endOfDueDay;
        }

        return facts.Now >= endOfDueDay || facts.PlannedEnds.Any(plannedEnd => plannedEnd > endOfDueDay);
    }
}

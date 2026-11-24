namespace TireScheduler.Engine;

// Conversions between plant-local dates and UTC instants (ARCHITECTURE.md section "Time rules").
public static class PlantTime
{
    // The first instant of a plant-local day, read as the end of the day before (A27).
    public static DateTimeOffset StartOfDay(DateOnly date, string timeZoneId) => EndOfDay(date.AddDays(-1), timeZoneId);

    // The first instant of the next plant-local day (DEC-22, A27).
    public static DateTimeOffset EndOfDay(DateOnly date, string timeZoneId)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return EarliestInstant(date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
    }

    // A plant-local wall-clock time as a UTC instant, read as EarliestInstant reads it (A27).
    public static DateTimeOffset LocalToUtc(DateTime localTime, string timeZoneId) =>
        EarliestInstant(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

    // A local time that occurs twice gives its first occurrence; one inside a DST gap gives the
    // instant the gap ends, the first local time that exists after it.
    internal static DateTimeOffset EarliestInstant(DateTime localTime, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(localTime))
        {
            return FirstInstantAtOrAfter(localTime, zone);
        }

        // The larger offset is the earlier of the two instants.
        TimeSpan offset = zone.IsAmbiguousTime(localTime) ? zone.GetAmbiguousTimeOffsets(localTime).Max() : zone.GetUtcOffset(localTime);
        return new DateTimeOffset(localTime, offset).ToUniversalTime();
    }

    // As EarliestInstant, but a local time that occurs twice gives its second occurrence, so a
    // working period ending then contains both (ARCHITECTURE.md section "Time rules").
    internal static DateTimeOffset LatestInstant(DateTime localTime, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(localTime))
        {
            return FirstInstantAtOrAfter(localTime, zone);
        }

        TimeSpan offset = zone.IsAmbiguousTime(localTime) ? zone.GetAmbiguousTimeOffsets(localTime).Min() : zone.GetUtcOffset(localTime);
        return new DateTimeOffset(localTime, offset).ToUniversalTime();
    }

    // The plant-local date that holds the instant; the dashboard's today (A49).
    public static DateOnly DateOf(DateTimeOffset instant, string timeZoneId) =>
        LocalDate(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

    internal static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    // For a local time inside a DST gap: the instant the gap ends. Read with the offset after the
    // gap, the local time is an instant before the gap; read with the offset before it, one after.
    // Half a day either side of the gap holds no other offset change.
    private static DateTimeOffset FirstInstantAtOrAfter(DateTime localTimeInGap, TimeZoneInfo zone)
    {
        TimeSpan offsetBefore = zone.GetUtcOffset(localTimeInGap.AddHours(-12));
        TimeSpan offsetAfter = zone.GetUtcOffset(localTimeInGap.AddHours(12));
        long earliestSecond = new DateTimeOffset(localTimeInGap, offsetAfter).ToUnixTimeSeconds();
        long latestSecond = new DateTimeOffset(localTimeInGap, offsetBefore).ToUnixTimeSeconds();
        while (earliestSecond < latestSecond)
        {
            long middleSecond = earliestSecond + ((latestSecond - earliestSecond) / 2);
            DateTime localTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(middleSecond), zone).DateTime;
            if (localTime >= localTimeInGap)
            {
                latestSecond = middleSecond;
            }
            else
            {
                earliestSecond = middleSecond + 1;
            }
        }

        return DateTimeOffset.FromUnixTimeSeconds(earliestSecond);
    }
}

namespace TireScheduler.Engine;

// Conversions between plant-local dates and UTC instants (ARCHITECTURE.md section "Time rules").
public static class PlantTime
{
    // The first instant of the next plant-local day (DEC-22, A27).
    public static DateTimeOffset EndOfDay(DateOnly date, string timeZoneId)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateTime nextMidnight = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(nextMidnight))
        {
            return FirstInstantAtOrAfter(nextMidnight, zone);
        }

        // The larger offset is the earlier of the two instants.
        TimeSpan offset = zone.IsAmbiguousTime(nextMidnight)
            ? zone.GetAmbiguousTimeOffsets(nextMidnight).Max()
            : zone.GetUtcOffset(nextMidnight);
        return new DateTimeOffset(nextMidnight, offset).ToUniversalTime();
    }

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

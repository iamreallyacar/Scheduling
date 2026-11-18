namespace TireScheduler.Api.Modules.MasterData.Calendars;

// One requested shift, with its position in the request so errors can name its field path.
internal readonly record struct RequestedShift(int Position, DayOfWeek Weekday, int StartMinuteOfDay, int EndMinuteOfDay);

// The rules the shifts of a calendar must keep together (AC-42). No database: everything is passed in.
internal static class ShiftRules
{
    public static Dictionary<string, IReadOnlyList<string>> FindFieldErrors(IReadOnlyList<RequestedShift> shifts)
    {
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        foreach (RequestedShift shift in shifts.Where(shift => shift.StartMinuteOfDay >= shift.EndMinuteOfDay))
        {
            fieldErrors[$"shifts[{shift.Position}].end"] = ["The shift must end after it starts."];
        }

        // Sorted by start, a shift overlaps an earlier one of its day when it starts before the
        // latest end so far. Touching is allowed.
        foreach (IGrouping<DayOfWeek, RequestedShift> day in shifts.Where(shift => shift.StartMinuteOfDay < shift.EndMinuteOfDay).GroupBy(shift => shift.Weekday))
        {
            int latestEnd = 0;
            foreach (RequestedShift shift in day.OrderBy(shift => shift.StartMinuteOfDay).ThenBy(shift => shift.Position))
            {
                if (shift.StartMinuteOfDay < latestEnd)
                {
                    fieldErrors[$"shifts[{shift.Position}].start"] = [$"This shift overlaps another {day.Key} shift."];
                }

                latestEnd = Math.Max(latestEnd, shift.EndMinuteOfDay);
            }
        }

        return fieldErrors;
    }
}

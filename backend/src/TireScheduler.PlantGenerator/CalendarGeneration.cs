namespace TireScheduler.PlantGenerator;

internal static class CalendarGeneration
{
    // Weekdays in the order of the parameters, and the shifts of each in the order given.
    public static GeneratedShift[] Shifts(PlantParameters parameters) =>
        parameters.WorkingDays
            .SelectMany(weekday => parameters.Shifts.Select(shift => new GeneratedShift(weekday, shift.StartMinute, shift.EndMinute)))
            .ToArray();

    public static List<DateOnly> WorkingDates(PlantParameters parameters, DateOnly startDate) =>
        Enumerable.Range(0, parameters.HorizonWeeks * 7)
            .Select(startDate.AddDays)
            .Where(date => parameters.WorkingDays.Contains(date.DayOfWeek))
            .ToList();

    public static GeneratedHoliday[] Holidays(PlantParameters parameters, DateOnly startDate, SeededRandom random)
    {
        List<DateOnly> candidates = WorkingDates(parameters, startDate);
        // A partial Fisher-Yates shuffle picks distinct days.
        for (int position = 0; position < parameters.HolidayCount; position++)
        {
            int chosen = random.Next(position, candidates.Count);
            (candidates[position], candidates[chosen]) = (candidates[chosen], candidates[position]);
        }

        return candidates
            .Take(parameters.HolidayCount)
            .Order()
            .Select((date, position) => new GeneratedHoliday(date, $"Plant holiday {position + 1}"))
            .ToArray();
    }
}

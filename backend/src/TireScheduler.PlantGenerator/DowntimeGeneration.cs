namespace TireScheduler.PlantGenerator;

// Planned maintenance and breakdowns per machine and week (A33): starts from 06:00 to 18:00 on
// the 15-minute grid of a working day, at least an hour apart on one machine. A candidate that
// would come closer to one already drawn is left out, so an open-ended breakdown is always the
// last of its machine.
internal static class DowntimeGeneration
{
    private const int FirstStartMinute = 6 * 60;
    private const int GridMinutes = 15;
    private const int GridStepsToLastStart = (18 * 60 - FirstStartMinute) / GridMinutes;
    private const int MinimumGapMinutes = 60;

    public static GeneratedUnavailability[] Unavailabilities(PlantParameters parameters, DateOnly startDate, IReadOnlyList<GeneratedMachine> machines, SeededRandom random)
    {
        List<DateOnly> workingDates = CalendarGeneration.WorkingDates(parameters, startDate);
        List<GeneratedUnavailability> generated = [];
        foreach (GeneratedMachine machine in machines)
        {
            List<GeneratedUnavailability> ofMachine = [];
            for (int week = 0; week < parameters.HorizonWeeks; week++)
            {
                DateOnly weekStart = startDate.AddDays(week * 7);
                DateOnly[] daysOfWeek = workingDates.Where(date => date >= weekStart && date < weekStart.AddDays(7)).ToArray();
                if (daysOfWeek.Length == 0)
                {
                    continue;
                }

                AddIfApart(ofMachine, Draw(machine, UnavailabilityKind.Planned, parameters.PlannedMaintenance, openEndedShare: 0, daysOfWeek, random));
                AddIfApart(ofMachine, Draw(machine, UnavailabilityKind.Breakdown, parameters.Breakdowns, parameters.OpenEndedBreakdownShare, daysOfWeek, random));
            }

            generated.AddRange(ofMachine.OrderBy(unavailability => unavailability.LocalStart));
        }

        return [.. generated];
    }

    private static GeneratedUnavailability? Draw(
        GeneratedMachine machine,
        UnavailabilityKind kind,
        DowntimeParameters downtime,
        double openEndedShare,
        DateOnly[] daysOfWeek,
        SeededRandom random)
    {
        if (!random.Chance(downtime.ChancePerMachineWeek))
        {
            return null;
        }

        DateOnly day = random.Pick(daysOfWeek);
        int startMinute = FirstStartMinute + (GridMinutes * random.Next(GridStepsToLastStart + 1));
        int duration = random.Draw(downtime.DurationMinutes);
        int? durationMinutes = random.Chance(openEndedShare) ? null : duration;
        string reason = kind == UnavailabilityKind.Planned ? "Planned maintenance" : "Breakdown";
        return new GeneratedUnavailability(machine.Name, kind, day.ToDateTime(TimeOnly.MinValue).AddMinutes(startMinute), durationMinutes, reason);
    }

    private static void AddIfApart(List<GeneratedUnavailability> ofMachine, GeneratedUnavailability? candidate)
    {
        if (candidate is not null && ofMachine.All(existing => AreApart(existing, candidate)))
        {
            ofMachine.Add(candidate);
        }
    }

    private static bool AreApart(GeneratedUnavailability first, GeneratedUnavailability second) =>
        EndsAnHourBefore(first, second) || EndsAnHourBefore(second, first);

    private static bool EndsAnHourBefore(GeneratedUnavailability earlier, GeneratedUnavailability later) =>
        earlier.DurationMinutes is { } duration && earlier.LocalStart.AddMinutes(duration + MinimumGapMinutes) <= later.LocalStart;
}

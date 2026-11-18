namespace TireScheduler.PlantGenerator;

// AC-156: parameters outside their limits fail before anything is generated, naming the
// parameter by its path, for example "RoutingStages[1].MachineTypeName".
internal static class PlantParameterChecks
{
    private const int MinutesPerDay = 24 * 60;

    public static void ThrowIfInvalid(PlantParameters parameters, DateOnly startDate)
    {
        CheckMachineTypes(parameters.MachineTypes);
        AtLeast(parameters.ProductCount, 1, nameof(PlantParameters.ProductCount));
        CheckRouting(parameters);
        // Before the calendar, whose holiday count depends on the horizon.
        AtLeast(parameters.HorizonWeeks, 1, nameof(PlantParameters.HorizonWeeks));
        CheckCalendar(parameters, startDate);
        AtLeast(parameters.OrdersPerWeek, 0, nameof(PlantParameters.OrdersPerWeek));
        Range(parameters.Quantity, smallest: 1, nameof(PlantParameters.Quantity));
        Range(parameters.DueDays, smallest: 0, nameof(PlantParameters.DueDays));
        Share(parameters.RushShare, nameof(PlantParameters.RushShare));
        Range(parameters.RushDueDays, smallest: 0, nameof(PlantParameters.RushDueDays));
        Share(parameters.LowPriorityShare, nameof(PlantParameters.LowPriorityShare));
        Downtime(parameters.PlannedMaintenance, nameof(PlantParameters.PlannedMaintenance));
        Downtime(parameters.Breakdowns, nameof(PlantParameters.Breakdowns));
        Share(parameters.OpenEndedBreakdownShare, nameof(PlantParameters.OpenEndedBreakdownShare));
    }

    private static void CheckMachineTypes(IReadOnlyList<MachineTypeParameters> machineTypes)
    {
        AtLeast(machineTypes.Count, 1, nameof(PlantParameters.MachineTypes));
        if (machineTypes.Select(type => type.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != machineTypes.Count)
        {
            throw Invalid(nameof(PlantParameters.MachineTypes), "Machine type names must differ.");
        }

        for (int position = 0; position < machineTypes.Count; position++)
        {
            string path = $"{nameof(PlantParameters.MachineTypes)}[{position}]";
            NotBlank(machineTypes[position].Name, $"{path}.{nameof(MachineTypeParameters.Name)}");
            NotBlank(machineTypes[position].MachineNamePrefix, $"{path}.{nameof(MachineTypeParameters.MachineNamePrefix)}");
            AtLeast(machineTypes[position].MachineCount, 1, $"{path}.{nameof(MachineTypeParameters.MachineCount)}");
        }
    }

    private static void CheckRouting(PlantParameters parameters)
    {
        AtLeast(parameters.RoutingStages.Count, 1, nameof(PlantParameters.RoutingStages));
        HashSet<string> machineTypeNames = parameters.MachineTypes.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        for (int position = 0; position < parameters.RoutingStages.Count; position++)
        {
            StageParameters stage = parameters.RoutingStages[position];
            string path = $"{nameof(PlantParameters.RoutingStages)}[{position}]";
            if (!machineTypeNames.Contains(stage.MachineTypeName))
            {
                throw Invalid($"{path}.{nameof(StageParameters.MachineTypeName)}", $"'{stage.MachineTypeName}' is not one of the machine types.");
            }

            Range(stage.SetupMinutes, smallest: 0, $"{path}.{nameof(StageParameters.SetupMinutes)}");
            Range(stage.CycleMinutes, smallest: 1, $"{path}.{nameof(StageParameters.CycleMinutes)}");
            Range(stage.UnitsPerCycle, smallest: 1, $"{path}.{nameof(StageParameters.UnitsPerCycle)}");
        }

        if (parameters.StageLagMinutes.Count != parameters.RoutingStages.Count - 1)
        {
            throw Invalid(nameof(PlantParameters.StageLagMinutes), "There must be one lag for each pair of consecutive stages.");
        }

        for (int position = 0; position < parameters.StageLagMinutes.Count; position++)
        {
            Range(parameters.StageLagMinutes[position], smallest: 0, $"{nameof(PlantParameters.StageLagMinutes)}[{position}]");
        }
    }

    private static void CheckCalendar(PlantParameters parameters, DateOnly startDate)
    {
        AtLeast(parameters.WorkingDays.Count, 1, nameof(PlantParameters.WorkingDays));
        if (parameters.WorkingDays.Distinct().Count() != parameters.WorkingDays.Count)
        {
            throw Invalid(nameof(PlantParameters.WorkingDays), "A working day is listed twice.");
        }

        AtLeast(parameters.Shifts.Count, 1, nameof(PlantParameters.Shifts));
        for (int position = 0; position < parameters.Shifts.Count; position++)
        {
            ShiftParameters shift = parameters.Shifts[position];
            if (shift.StartMinute < 0 || shift.EndMinute > MinutesPerDay || shift.EndMinute <= shift.StartMinute)
            {
                throw Invalid($"{nameof(PlantParameters.Shifts)}[{position}]", $"A shift lies inside one day (0 to {MinutesPerDay} minutes) and ends after it starts.");
            }
        }

        ShiftParameters[] inStartOrder = parameters.Shifts.OrderBy(shift => shift.StartMinute).ToArray();
        if (inStartOrder.Zip(inStartOrder.Skip(1)).Any(pair => pair.Second.StartMinute < pair.First.EndMinute))
        {
            throw Invalid(nameof(PlantParameters.Shifts), "Shifts must not overlap.");
        }

        int workingDays = CalendarGeneration.WorkingDates(parameters, startDate).Count;
        if (parameters.HolidayCount < 0 || parameters.HolidayCount > workingDays)
        {
            throw Invalid(nameof(PlantParameters.HolidayCount), $"Must be from 0 to the {workingDays} working days of the horizon, not {parameters.HolidayCount}.");
        }
    }

    private static void Downtime(DowntimeParameters downtime, string path)
    {
        Share(downtime.ChancePerMachineWeek, $"{path}.{nameof(DowntimeParameters.ChancePerMachineWeek)}");
        Range(downtime.DurationMinutes, smallest: 1, $"{path}.{nameof(DowntimeParameters.DurationMinutes)}");
    }

    private static void AtLeast(int value, int smallest, string path)
    {
        if (value < smallest)
        {
            throw Invalid(path, $"Must be at least {smallest}, not {value}.");
        }
    }

    private static void Range(ValueRange range, int smallest, string path)
    {
        if (range.Min < smallest || range.Max < range.Min)
        {
            throw Invalid(path, $"The minimum must be at least {smallest} and not above the maximum, not {range}.");
        }
    }

    // Written so that NaN fails too.
    private static void Share(double share, string path)
    {
        if (!(share >= 0 && share <= 1))
        {
            throw Invalid(path, $"Must be from 0 to 1, not {share}.");
        }
    }

    private static void NotBlank(string text, string path)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw Invalid(path, "Must not be empty.");
        }
    }

    // The parameter name is the path inside PlantParameters, not a parameter of these methods.
    private static ArgumentException Invalid(string path, string message) => new(message, path);
}

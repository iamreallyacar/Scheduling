namespace TireScheduler.PlantGenerator.Tests;

public sealed class GeneratedPlantTests
{
    [Theory]
    [MemberData(nameof(GeneratorCases.PresetsWithSeeds), MemberType = typeof(GeneratorCases))]
    public void AC_152_machines_routings_and_orders_refer_to_generated_rows_with_values_inside_their_ranges(string presetName, int seed, DateOnly startDate)
    {
        PlantParameters parameters = PlantPresets.ByName[presetName];
        GeneratedPlant plant = SyntheticPlant.Generate(parameters, seed, startDate);

        Assert.Equal(parameters.MachineTypes.Select(type => type.Name), plant.MachineTypes.Select(type => type.Name));
        foreach (MachineTypeParameters type in parameters.MachineTypes)
        {
            Assert.Equal(type.MachineCount, plant.Machines.Count(machine => machine.MachineTypeName == type.Name));
        }

        Assert.Equal(plant.Machines.Count, plant.Machines.Select(machine => machine.Name).Distinct().Count());
        Assert.Equal(parameters.ProductCount, plant.Products.Count);
        Assert.Equal(plant.Products.Count, plant.Products.Select(product => product.Code).Distinct().Count());
        foreach (GeneratedProduct product in plant.Products)
        {
            AssertRoutingFollowsParameters(parameters, product);
        }

        HashSet<string> productCodes = plant.Products.Select(product => product.Code).ToHashSet();
        Assert.Equal(plant.Orders.Count, plant.Orders.Select(order => order.OrderNo).Distinct().Count());
        Assert.All(plant.Orders, order =>
        {
            Assert.Contains(order.ProductCode, productCodes);
            AssertInside(parameters.Quantity, order.Quantity);
        });
    }

    [Theory]
    [MemberData(nameof(GeneratorCases.PresetsWithSeeds), MemberType = typeof(GeneratorCases))]
    public void AC_153_shifts_holidays_and_orders_follow_the_calendar_and_order_parameters(string presetName, int seed, DateOnly startDate)
    {
        PlantParameters parameters = PlantPresets.ByName[presetName];
        GeneratedPlant plant = SyntheticPlant.Generate(parameters, seed, startDate);

        GeneratedShift[] expectedShifts = parameters.WorkingDays
            .SelectMany(weekday => parameters.Shifts.Select(shift => new GeneratedShift(weekday, shift.StartMinute, shift.EndMinute)))
            .ToArray();
        Assert.Equal(expectedShifts, plant.Shifts);

        Assert.Equal(parameters.HolidayCount, plant.Holidays.Select(holiday => holiday.Date).Distinct().Count());
        Assert.Equal(parameters.HolidayCount, plant.Holidays.Count);
        Assert.All(plant.Holidays, holiday =>
        {
            Assert.InRange(holiday.Date, startDate, startDate.AddDays((parameters.HorizonWeeks * 7) - 1));
            Assert.Contains(holiday.Date.DayOfWeek, parameters.WorkingDays);
        });

        DateOnly[] weekStarts = Enumerable.Range(0, parameters.HorizonWeeks).Select(week => startDate.AddDays(week * 7)).ToArray();
        Assert.Equal(weekStarts, plant.Orders.Select(order => order.WeekStart).Distinct().Order());
        Assert.All(plant.Orders.GroupBy(order => order.WeekStart), week => Assert.Equal(parameters.OrdersPerWeek, week.Count()));
        Assert.All(plant.Orders, order =>
        {
            AssertInside(order.IsRush ? parameters.RushDueDays : parameters.DueDays, order.DueDate.DayNumber - order.WeekStart.DayNumber);
            if (order.IsRush)
            {
                Assert.Equal(GeneratedPriority.High, order.Priority);
            }
            else
            {
                Assert.Contains(order.Priority, (GeneratedPriority[])[GeneratedPriority.Normal, GeneratedPriority.Low]);
            }
        });
    }

    [Theory]
    [MemberData(nameof(GeneratorCases.PresetsWithSeeds), MemberType = typeof(GeneratorCases))]
    public void AC_154_unavailabilities_start_on_the_grid_of_working_days_and_never_come_closer_than_an_hour(string presetName, int seed, DateOnly startDate)
    {
        PlantParameters parameters = PlantPresets.ByName[presetName];
        GeneratedPlant plant = SyntheticPlant.Generate(parameters, seed, startDate);

        HashSet<string> machineNames = plant.Machines.Select(machine => machine.Name).ToHashSet();
        Assert.All(plant.Unavailabilities, unavailability =>
        {
            Assert.Contains(unavailability.MachineName, machineNames);
            DateOnly day = DateOnly.FromDateTime(unavailability.LocalStart);
            Assert.InRange(day, startDate, plant.EndDate.AddDays(-1));
            Assert.Contains(day.DayOfWeek, parameters.WorkingDays);
            int startMinute = (int)unavailability.LocalStart.TimeOfDay.TotalMinutes;
            Assert.Equal(TimeSpan.FromMinutes(startMinute), unavailability.LocalStart.TimeOfDay);
            Assert.InRange(startMinute, 6 * 60, 18 * 60);
            Assert.Equal(0, startMinute % 15);
            DowntimeParameters kind = unavailability.Kind == UnavailabilityKind.Planned ? parameters.PlannedMaintenance : parameters.Breakdowns;
            if (unavailability.DurationMinutes is { } duration)
            {
                AssertInside(kind.DurationMinutes, duration);
            }
            else
            {
                Assert.Equal(UnavailabilityKind.Breakdown, unavailability.Kind);
                Assert.True(parameters.OpenEndedBreakdownShare > 0);
            }
        });

        foreach (IGrouping<string, GeneratedUnavailability> machine in plant.Unavailabilities.GroupBy(unavailability => unavailability.MachineName))
        {
            GeneratedUnavailability[] inStartOrder = machine.OrderBy(unavailability => unavailability.LocalStart).ToArray();
            for (int position = 1; position < inStartOrder.Length; position++)
            {
                GeneratedUnavailability earlier = inStartOrder[position - 1];
                Assert.NotNull(earlier.DurationMinutes);
                Assert.True(
                    earlier.LocalStart.AddMinutes(earlier.DurationMinutes.Value + 60) <= inStartOrder[position].LocalStart,
                    $"{machine.Key}: {earlier.LocalStart} for {earlier.DurationMinutes} minutes is closer than an hour to {inStartOrder[position].LocalStart}.");
            }
        }
    }

    [Fact]
    public void AC_154_the_small_preset_has_both_kinds_and_open_ended_breakdowns_over_some_seeds()
    {
        GeneratedUnavailability[] unavailabilities = Enumerable.Range(1, 10)
            .SelectMany(seed => SyntheticPlant.Generate(PlantPresets.Small, seed, GeneratorCases.StartMonday).Unavailabilities)
            .ToArray();

        Assert.Contains(unavailabilities, unavailability => unavailability.Kind == UnavailabilityKind.Planned);
        Assert.Contains(unavailabilities, unavailability => unavailability.Kind == UnavailabilityKind.Breakdown && unavailability.DurationMinutes is not null);
        Assert.Contains(unavailabilities, unavailability => unavailability.DurationMinutes is null);
    }

    private static void AssertRoutingFollowsParameters(PlantParameters parameters, GeneratedProduct product)
    {
        Assert.Equal(parameters.RoutingStages.Count, product.Stages.Count);
        for (int position = 0; position < product.Stages.Count; position++)
        {
            StageParameters expected = parameters.RoutingStages[position];
            GeneratedStage stage = product.Stages[position];
            Assert.Equal(position + 1, stage.SequenceNo);
            Assert.Equal(expected.MachineTypeName, stage.MachineTypeName);
            AssertInside(expected.SetupMinutes, stage.SetupMinutes);
            AssertInside(expected.CycleMinutes, stage.CycleMinutes);
            AssertInside(expected.UnitsPerCycle, stage.UnitsPerCycle);
        }

        Assert.Equal(product.Stages.Count - 1, product.Dependencies.Count);
        for (int position = 0; position < product.Dependencies.Count; position++)
        {
            GeneratedDependency dependency = product.Dependencies[position];
            Assert.Equal((position + 1, position + 2), (dependency.PredecessorSequenceNo, dependency.SuccessorSequenceNo));
            AssertInside(parameters.StageLagMinutes[position], dependency.MinLagMinutes);
        }
    }

    private static void AssertInside(ValueRange range, int value) => Assert.InRange(value, range.Min, range.Max);
}

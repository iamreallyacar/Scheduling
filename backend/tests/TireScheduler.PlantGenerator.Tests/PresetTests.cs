namespace TireScheduler.PlantGenerator.Tests;

public sealed class PresetTests
{
    private const string Berlin = "Europe/Berlin";

    [Fact]
    public void AC_155_the_small_preset_has_4_machines_of_3_types_and_3_products()
    {
        GeneratedPlant plant = SyntheticPlant.Generate(PlantPresets.Small, seed: 1, GeneratorCases.StartMonday);

        Assert.Equal((4, 3, 3), (plant.Machines.Count, plant.MachineTypes.Count, plant.Products.Count));
    }

    [Fact]
    public void AC_155_the_plant_preset_has_20_machines_of_4_types_40_products_and_three_shifts_from_Monday_to_Friday()
    {
        GeneratedPlant plant = SyntheticPlant.Generate(PlantPresets.Plant, seed: 1, GeneratorCases.StartMonday);

        Assert.Equal(20, plant.Machines.Count);
        Assert.Equal(["Extrusion line", "Building machine", "Curing press", "Inspection station"], plant.MachineTypes.Select(type => type.Name));
        Assert.Equal(40, plant.Products.Count);
        DayOfWeek[] mondayToFriday = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        GeneratedShift[] expectedShifts = mondayToFriday
            .SelectMany(weekday => new GeneratedShift[] { new(weekday, 0, 480), new(weekday, 480, 960), new(weekday, 960, 1440) })
            .ToArray();
        Assert.Equal(expectedShifts, plant.Shifts);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void AC_155_the_stress_preset_has_25_machines_over_2_weeks_and_more_work_than_working_time_on_every_type(int seed)
    {
        GeneratedPlant plant = SyntheticPlant.Generate(PlantPresets.Stress, seed, GeneratorCases.StartMonday);

        Assert.Equal((25, 2), (plant.Machines.Count, plant.HorizonWeeks));
        Assert.All(PlantLoad.Measure(plant, Berlin), load =>
            Assert.True(load.DemandMinutes >= load.CapacityMinutes, $"{load.MachineTypeName}: {load.DemandMinutes} of {load.CapacityMinutes} minutes."));
    }

    [Fact]
    public void The_load_counts_every_lot_duration_and_the_shift_minutes_of_the_working_days_without_holidays()
    {
        GeneratedPlant plant = new(
            Seed: 1,
            StartDate: GeneratorCases.StartMonday,
            HorizonWeeks: 1,
            MachineTypes: [new("Curing press")],
            Machines: [new("Press 1", "Curing press"), new("Press 2", "Curing press")],
            Shifts: [new(DayOfWeek.Monday, 0, 480), new(DayOfWeek.Tuesday, 0, 480)],
            Holidays: [new(GeneratorCases.StartMonday.AddDays(1), "Holiday")],
            Products: [new("TR-1", "Tire", "Spec", [new(1, "Curing press", 30, 12, 2)], [])],
            Orders: [new("SO-1", "Customer", "TR-1", 101, GeneratorCases.StartMonday, GeneratorCases.StartMonday.AddDays(7), GeneratedPriority.Normal, IsRush: false)],
            Unavailabilities: []);

        Assert.Equal([new MachineTypeLoad("Curing press", DemandMinutes: 642, CapacityMinutes: 2 * 480)], PlantLoad.Measure(plant, Berlin));
    }

    // A34: the working time is the engine's, so the spring-forward Sunday of 2027-03-28 has 7 hours
    // from 00:00 to 08:00.
    [Fact]
    public void The_load_counts_the_elapsed_working_minutes_of_a_DST_day()
    {
        DateOnly springForwardWeek = new(2027, 3, 22);
        GeneratedPlant plant = new(
            Seed: 1,
            StartDate: springForwardWeek,
            HorizonWeeks: 1,
            MachineTypes: [new("Curing press")],
            Machines: [new("Press 1", "Curing press")],
            Shifts: [new(DayOfWeek.Saturday, 0, 480), new(DayOfWeek.Sunday, 0, 480)],
            Holidays: [],
            Products: [new("TR-1", "Tire", "Spec", [new(1, "Curing press", 30, 12, 2)], [])],
            Orders: [],
            Unavailabilities: []);

        Assert.Equal([new MachineTypeLoad("Curing press", DemandMinutes: 0, CapacityMinutes: 480 + 420)], PlantLoad.Measure(plant, Berlin));
    }

    public static TheoryData<int> Seeds() => [.. Enumerable.Range(1, 20)];
}

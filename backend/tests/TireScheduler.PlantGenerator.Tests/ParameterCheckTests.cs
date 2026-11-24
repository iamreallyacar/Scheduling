namespace TireScheduler.PlantGenerator.Tests;

public sealed class ParameterCheckTests
{
    [Theory]
    [InlineData("MachineTypes")]
    [InlineData("MachineTypes[0].MachineCount")]
    [InlineData("ProductCount")]
    [InlineData("RoutingStages")]
    [InlineData("RoutingStages[1].MachineTypeName")]
    [InlineData("RoutingStages[0].CycleMinutes")]
    [InlineData("RoutingStages[2].SetupMinutes")]
    [InlineData("StageLagMinutes")]
    [InlineData("StageLagMinutes[1]")]
    [InlineData("WorkingDays")]
    [InlineData("Shifts[0]")]
    [InlineData("Shifts[2]")]
    [InlineData("HolidayCount")]
    [InlineData("HorizonWeeks")]
    [InlineData("OrdersPerWeek")]
    [InlineData("Quantity")]
    [InlineData("DueDays")]
    [InlineData("RushShare")]
    [InlineData("LowPriorityShare")]
    [InlineData("PlannedMaintenance.DurationMinutes")]
    [InlineData("Breakdowns.ChancePerMachineWeek")]
    [InlineData("OpenEndedBreakdownShare")]
    public void AC_156_parameters_outside_their_limits_fail_naming_the_parameter(string parameterName)
    {
        PlantParameters broken = Break(PlantPresets.Small, parameterName);

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() => SyntheticPlant.Generate(broken, seed: 1, GeneratorCases.StartMonday));

        Assert.Equal(parameterName, failure.ParamName);
    }

    [Theory]
    [InlineData("Press", "Press")]
    [InlineData("Press", "press")]
    [InlineData("Press", "  Press ")]
    public void AC_156_machine_name_prefixes_that_the_api_would_see_as_equal_fail_naming_the_second(string first, string second)
    {
        PlantParameters broken = PlantPresets.Small with
        {
            MachineTypes = [PlantPresets.Small.MachineTypes[0] with { MachineNamePrefix = first }, PlantPresets.Small.MachineTypes[1] with { MachineNamePrefix = second }, .. PlantPresets.Small.MachineTypes.Skip(2)],
        };

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() => SyntheticPlant.Generate(broken, seed: 1, GeneratorCases.StartMonday));

        Assert.Equal("MachineTypes[1].MachineNamePrefix", failure.ParamName);
    }

    [Fact]
    public void AC_156_more_holidays_than_working_days_in_the_horizon_fail_naming_the_holiday_count()
    {
        // The small preset has 2 weeks of 5 working days.
        PlantParameters broken = PlantPresets.Small with { HolidayCount = 11 };

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() => SyntheticPlant.Generate(broken, seed: 1, GeneratorCases.StartMonday));

        Assert.Equal("HolidayCount", failure.ParamName);
    }

    [Fact]
    public void AC_156_the_smallest_allowed_values_generate_a_plant()
    {
        PlantParameters smallest = PlantPresets.Small with
        {
            OrdersPerWeek = 0,
            HolidayCount = 0,
            RushShare = 0,
            LowPriorityShare = 1,
            OpenEndedBreakdownShare = 1,
            Quantity = new(1, 1),
            DueDays = new(0, 0),
        };

        GeneratedPlant plant = SyntheticPlant.Generate(smallest, seed: 1, GeneratorCases.StartMonday);

        Assert.Empty(plant.Orders);
    }

    private static PlantParameters Break(PlantParameters valid, string parameterName) => parameterName switch
    {
        "MachineTypes" => valid with { MachineTypes = [] },
        "MachineTypes[0].MachineCount" => valid with { MachineTypes = [valid.MachineTypes[0] with { MachineCount = 0 }, .. valid.MachineTypes.Skip(1)] },
        "ProductCount" => valid with { ProductCount = 0 },
        "RoutingStages" => valid with { RoutingStages = [], StageLagMinutes = [] },
        "RoutingStages[1].MachineTypeName" => valid with { RoutingStages = ReplaceStage(valid, 1, stage => stage with { MachineTypeName = "Unknown type" }) },
        "RoutingStages[0].CycleMinutes" => valid with { RoutingStages = ReplaceStage(valid, 0, stage => stage with { CycleMinutes = new(0, 2) }) },
        "RoutingStages[2].SetupMinutes" => valid with { RoutingStages = ReplaceStage(valid, 2, stage => stage with { SetupMinutes = new(20, 10) }) },
        "StageLagMinutes" => valid with { StageLagMinutes = [new(0, 0)] },
        "StageLagMinutes[1]" => valid with { StageLagMinutes = [valid.StageLagMinutes[0], new(-1, 10)] },
        "WorkingDays" => valid with { WorkingDays = [] },
        "Shifts[0]" => valid with { Shifts = [new(480, 480), .. valid.Shifts.Skip(1)] },
        "Shifts[2]" => valid with { Shifts = [.. valid.Shifts.Take(2), new(960, 1441)] },
        "HolidayCount" => valid with { HolidayCount = -1 },
        "HorizonWeeks" => valid with { HorizonWeeks = 0 },
        "OrdersPerWeek" => valid with { OrdersPerWeek = -1 },
        "Quantity" => valid with { Quantity = new(0, 10) },
        "DueDays" => valid with { DueDays = new(5, 4) },
        "RushShare" => valid with { RushShare = 1.5 },
        "LowPriorityShare" => valid with { LowPriorityShare = -0.1 },
        "PlannedMaintenance.DurationMinutes" => valid with { PlannedMaintenance = valid.PlannedMaintenance with { DurationMinutes = new(0, 60) } },
        "Breakdowns.ChancePerMachineWeek" => valid with { Breakdowns = valid.Breakdowns with { ChancePerMachineWeek = 2 } },
        "OpenEndedBreakdownShare" => valid with { OpenEndedBreakdownShare = double.NaN },
        _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, "No broken value for this parameter."),
    };

    private static StageParameters[] ReplaceStage(PlantParameters valid, int position, Func<StageParameters, StageParameters> change) =>
        valid.RoutingStages.Select((stage, index) => index == position ? change(stage) : stage).ToArray();
}

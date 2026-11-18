namespace TireScheduler.PlantGenerator;

// The named parameter sets of TASKS.md section "M2-7 synthetic plant generator". Their values
// follow the business assumptions of A30; an answer to BQ2 to BQ4 changes only these values.
public static class PlantPresets
{
    private static readonly DayOfWeek[] MondayToFriday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // The plant default calendar of A3.
    private static readonly ShiftParameters[] ThreeEightHourShifts =
        [new(0, 480), new(480, 960), new(960, 1440)];

    private static readonly MachineTypeParameters Extrusion = new("Extrusion line", "Extruder", 1);
    private static readonly MachineTypeParameters Building = new("Building machine", "Builder", 1);
    private static readonly MachineTypeParameters Curing = new("Curing press", "Press", 1);
    private static readonly MachineTypeParameters Inspection = new("Inspection station", "Inspection", 1);

    // Green tire rest before curing and cooling before inspection (A30).
    private static readonly ValueRange BuildingToCuringLag = new(60, 240);
    private static readonly ValueRange CuringToInspectionLag = new(0, 30);

    // A few machines and products, with frequent and open-ended downtime, for tests.
    public static PlantParameters Small { get; } = new()
    {
        MachineTypes = [Extrusion, Building, Curing with { MachineCount = 2 }],
        ProductCount = 3,
        RoutingStages =
        [
            new(Extrusion.Name, SetupMinutes: new(0, 15), CycleMinutes: new(1, 2), UnitsPerCycle: new(1, 1)),
            new(Building.Name, SetupMinutes: new(0, 10), CycleMinutes: new(2, 4), UnitsPerCycle: new(1, 1)),
            new(Curing.Name, SetupMinutes: new(10, 30), CycleMinutes: new(10, 14), UnitsPerCycle: new(2, 2)),
        ],
        StageLagMinutes = [new(0, 60), BuildingToCuringLag],
        WorkingDays = MondayToFriday,
        Shifts = ThreeEightHourShifts,
        HolidayCount = 1,
        HorizonWeeks = 2,
        OrdersPerWeek = 4,
        Quantity = new(10, 60),
        DueDays = new(5, 14),
        RushShare = 0.25,
        RushDueDays = new(2, 4),
        LowPriorityShare = 0.25,
        PlannedMaintenance = new(ChancePerMachineWeek: 0.5, DurationMinutes: new(60, 240)),
        Breakdowns = new(ChancePerMachineWeek: 0.25, DurationMinutes: new(30, 240)),
        OpenEndedBreakdownShare = 0.2,
    };

    // The scenario of README.md: about 20 machines, about 40 products, 3 shifts, about 160 lots
    // a week, with every machine type loaded to roughly two thirds or three quarters.
    public static PlantParameters Plant { get; } = new()
    {
        MachineTypes =
        [
            Extrusion with { MachineCount = 3 },
            Building with { MachineCount = 5 },
            Curing with { MachineCount = 10 },
            Inspection with { MachineCount = 2 },
        ],
        ProductCount = 40,
        RoutingStages =
        [
            new(Extrusion.Name, SetupMinutes: new(15, 30), CycleMinutes: new(1, 2), UnitsPerCycle: new(1, 1)),
            new(Building.Name, SetupMinutes: new(10, 20), CycleMinutes: new(2, 4), UnitsPerCycle: new(1, 1)),
            new(Curing.Name, SetupMinutes: new(20, 40), CycleMinutes: new(10, 14), UnitsPerCycle: new(2, 2)),
            new(Inspection.Name, SetupMinutes: new(0, 0), CycleMinutes: new(1, 1), UnitsPerCycle: new(1, 1)),
        ],
        StageLagMinutes = [new(0, 0), BuildingToCuringLag, CuringToInspectionLag],
        WorkingDays = MondayToFriday,
        Shifts = ThreeEightHourShifts,
        HolidayCount = 2,
        HorizonWeeks = 8,
        OrdersPerWeek = 40,
        Quantity = new(50, 400),
        DueDays = new(10, 28),
        RushShare = 0.1,
        RushDueDays = new(3, 7),
        LowPriorityShare = 0.2,
        PlannedMaintenance = new(ChancePerMachineWeek: 0.25, DurationMinutes: new(120, 480)),
        Breakdowns = new(ChancePerMachineWeek: 0.1, DurationMinutes: new(60, 480)),
        OpenEndedBreakdownShare = 0,
    };

    // 25 machines over 2 weeks with more work than they can do (F-NFR-5, A34): every machine
    // type needs about 115 % of its working time.
    public static PlantParameters Stress { get; } = new()
    {
        MachineTypes =
        [
            Extrusion with { MachineCount = 4 },
            Building with { MachineCount = 7 },
            Curing with { MachineCount = 12 },
            Inspection with { MachineCount = 2 },
        ],
        ProductCount = 40,
        RoutingStages =
        [
            new(Extrusion.Name, SetupMinutes: new(10, 20), CycleMinutes: new(2, 2), UnitsPerCycle: new(1, 1)),
            new(Building.Name, SetupMinutes: new(10, 20), CycleMinutes: new(3, 4), UnitsPerCycle: new(1, 1)),
            new(Curing.Name, SetupMinutes: new(20, 40), CycleMinutes: new(12, 12), UnitsPerCycle: new(2, 2)),
            new(Inspection.Name, SetupMinutes: new(0, 0), CycleMinutes: new(1, 1), UnitsPerCycle: new(1, 1)),
        ],
        StageLagMinutes = [new(0, 0), BuildingToCuringLag, CuringToInspectionLag],
        WorkingDays = MondayToFriday,
        Shifts = ThreeEightHourShifts,
        HolidayCount = 0,
        HorizonWeeks = 2,
        OrdersPerWeek = 55,
        Quantity = new(200, 400),
        DueDays = new(3, 14),
        RushShare = 0.1,
        RushDueDays = new(2, 5),
        LowPriorityShare = 0.2,
        PlannedMaintenance = new(ChancePerMachineWeek: 0.25, DurationMinutes: new(120, 240)),
        Breakdowns = new(ChancePerMachineWeek: 0.1, DurationMinutes: new(60, 240)),
        OpenEndedBreakdownShare = 0,
    };

    public static IReadOnlyDictionary<string, PlantParameters> ByName { get; } = new Dictionary<string, PlantParameters>(StringComparer.Ordinal)
    {
        ["small"] = Small,
        ["plant"] = Plant,
        ["stress"] = Stress,
    };
}

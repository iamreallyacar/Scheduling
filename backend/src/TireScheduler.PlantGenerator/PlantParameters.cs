namespace TireScheduler.PlantGenerator;

// The inputs of a generated plant (TASKS.md section "M2-7 synthetic plant generator").
public sealed record PlantParameters
{
    public required IReadOnlyList<MachineTypeParameters> MachineTypes { get; init; }

    public required int ProductCount { get; init; }

    // Every product gets one stage per item, in this order.
    public required IReadOnlyList<StageParameters> RoutingStages { get; init; }

    // Item i is the lag from stage i + 1 to stage i + 2 of the linear chain.
    public required IReadOnlyList<ValueRange> StageLagMinutes { get; init; }

    public required IReadOnlyList<DayOfWeek> WorkingDays { get; init; }

    // The same shifts on every working day.
    public required IReadOnlyList<ShiftParameters> Shifts { get; init; }

    public required int HolidayCount { get; init; }

    public required int HorizonWeeks { get; init; }

    public required int OrdersPerWeek { get; init; }

    public required ValueRange Quantity { get; init; }

    // Days from the start of the order's week to its due date.
    public required ValueRange DueDays { get; init; }

    public required double RushShare { get; init; }

    public required ValueRange RushDueDays { get; init; }

    // Of the orders that are not rush orders.
    public required double LowPriorityShare { get; init; }

    public required DowntimeParameters PlannedMaintenance { get; init; }

    public required DowntimeParameters Breakdowns { get; init; }

    public required double OpenEndedBreakdownShare { get; init; }
}

public sealed record MachineTypeParameters(string Name, string MachineNamePrefix, int MachineCount);

public sealed record StageParameters(string MachineTypeName, ValueRange SetupMinutes, ValueRange CycleMinutes, ValueRange UnitsPerCycle);

// Minutes after plant-local midnight; an end of 1440 is the midnight that ends the day.
public sealed record ShiftParameters(int StartMinute, int EndMinute);

public sealed record DowntimeParameters(double ChancePerMachineWeek, ValueRange DurationMinutes);

// Both ends included.
public readonly record struct ValueRange(int Min, int Max);

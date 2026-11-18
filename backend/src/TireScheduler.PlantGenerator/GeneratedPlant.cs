using TireScheduler.Engine;

namespace TireScheduler.PlantGenerator;

// A generated plant as plain values. Machines, stages and orders name what they refer to,
// because ids exist only once the loader has written the rows.
public sealed record GeneratedPlant(
    int Seed,
    DateOnly StartDate,
    int HorizonWeeks,
    IReadOnlyList<GeneratedMachineType> MachineTypes,
    IReadOnlyList<GeneratedMachine> Machines,
    IReadOnlyList<GeneratedShift> Shifts,
    IReadOnlyList<GeneratedHoliday> Holidays,
    IReadOnlyList<GeneratedProduct> Products,
    IReadOnlyList<GeneratedOrder> Orders,
    IReadOnlyList<GeneratedUnavailability> Unavailabilities)
{
    public DateOnly EndDate => StartDate.AddDays(HorizonWeeks * 7);
}

public sealed record GeneratedMachineType(string Name);

public sealed record GeneratedMachine(string Name, string MachineTypeName);

public sealed record GeneratedShift(DayOfWeek Weekday, int StartMinute, int EndMinute);

public sealed record GeneratedHoliday(DateOnly Date, string Name);

public sealed record GeneratedProduct(
    string Code,
    string Name,
    string Spec,
    IReadOnlyList<GeneratedStage> Stages,
    IReadOnlyList<GeneratedDependency> Dependencies);

public sealed record GeneratedStage(int SequenceNo, string MachineTypeName, int SetupMinutes, int CycleMinutes, int UnitsPerCycle);

public sealed record GeneratedDependency(int PredecessorSequenceNo, int SuccessorSequenceNo, int MinLagMinutes);

public sealed record GeneratedOrder(
    string OrderNo,
    string CustomerName,
    string ProductCode,
    int Quantity,
    DateOnly WeekStart,
    DateOnly DueDate,
    GeneratedPriority Priority,
    bool IsRush);

public enum GeneratedPriority
{
    Low,
    Normal,
    High,
}

// A plant-local start and an elapsed duration; no duration means no known end (A33).
public sealed record GeneratedUnavailability(string MachineName, UnavailabilityKind Kind, DateTime LocalStart, int? DurationMinutes, string Reason)
{
    // Only the engine converts between plant-local time and UTC (ARCHITECTURE.md section "Time rules").
    public DateTimeOffset StartUtc(string timeZoneId) => PlantTime.LocalToUtc(LocalStart, timeZoneId);

    public DateTimeOffset? EndUtc(string timeZoneId) => DurationMinutes is { } minutes ? StartUtc(timeZoneId).AddMinutes(minutes) : null;
}

public enum UnavailabilityKind
{
    Planned,
    Breakdown,
}

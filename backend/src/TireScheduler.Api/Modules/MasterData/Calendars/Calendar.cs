namespace TireScheduler.Api.Modules.MasterData.Calendars;

// A weekly shift pattern. v1 has exactly one, the plant default, created by the first
// MasterData migration (A3, AC-40).
internal sealed class Calendar
{
    public const int NameMaxLength = 100;

    public int Id { get; private set; }

    public required string Name { get; set; }

    public bool IsPlantDefault { get; set; }

    public List<CalendarShift> Shifts { get; } = [];
}

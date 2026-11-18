namespace TireScheduler.Api.Modules.MasterData.Calendars;

// A plant-local date on which no calendar has working time (domain-model.md section 2).
internal sealed class Holiday
{
    public const int NameMaxLength = 100;

    public int Id { get; private set; }

    public DateOnly Date { get; set; }

    public required string Name { get; set; }
}

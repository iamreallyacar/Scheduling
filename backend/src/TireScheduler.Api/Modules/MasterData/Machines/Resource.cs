using TireScheduler.Api.Modules.MasterData.Calendars;

namespace TireScheduler.Api.Modules.MasterData.Machines;

// No status field: the displayed status is derived (DEC-9, DEC-12).
internal sealed class Resource
{
    public const int NameMaxLength = 100;

    public int Id { get; private set; }

    public required string Name { get; set; }

    public int ResourceTypeId { get; set; }

    public ResourceType? ResourceType { get; set; }

    public int CalendarId { get; set; }

    public Calendar? Calendar { get; set; }

    public bool IsActive { get; set; } = true;
}

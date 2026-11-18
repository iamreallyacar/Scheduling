namespace TireScheduler.Api.Modules.Scheduling;

// A set of placements (DEC-11). v1 has only the live version, created by the migration that adds
// this table (A41); scenarios (F-SCH-12) add more.
internal sealed class ScheduleVersion
{
    public const int NameMaxLength = 100;

    public int Id { get; private set; }

    public required string Name { get; set; }

    public bool IsLive { get; set; }
}

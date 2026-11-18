namespace TireScheduler.Api.Modules.Scheduling;

// A lot on a resource from its planned start to its planned end, in one schedule version
// (domain-model.md section 2). A lot has at most one placement per version (A40).
internal sealed class Placement
{
    public int Id { get; private set; }

    public int VersionId { get; init; }

    // An Orders lot and a MasterData resource. No foreign keys: modules do not reach into each
    // other's schemas.
    public int LotId { get; init; }

    public int ResourceId { get; set; }

    public DateTimeOffset PlannedStart { get; set; }

    // Computed from the resource's working time (R6) and stored, so reads need no calendar (A2).
    public DateTimeOffset PlannedEnd { get; set; }
}

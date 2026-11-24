namespace TireScheduler.Api.Modules.Progress;

// What the planner recorded of one lot (F-PRG-1). Actuals are facts, so they belong to no
// schedule version (domain-model.md section 2).
internal sealed class LotProgress
{
    // An Orders lot. No foreign key: modules do not reach into each other's schemas.
    public int LotId { get; init; }

    public DateTimeOffset ActualStart { get; set; }

    // Null while the lot runs.
    public DateTimeOffset? ActualEnd { get; set; }
}

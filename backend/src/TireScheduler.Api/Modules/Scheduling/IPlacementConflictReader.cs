namespace TireScheduler.Api.Modules.Scheduling;

// The public read interface of Scheduling for the conflict list in Queries (F-SCH-4, AC-183):
// the placements that break a rule after a later fact, checked by the engine's FindConflicts
// over the stored planned ends. `live` names the live version; one that does not exist is 404.
public interface IPlacementConflictReader
{
    // Sorted by planned start, then placement id.
    Task<IReadOnlyList<PlacementConflictValues>> FindConflictsAsync(string versionId, CancellationToken cancellationToken);
}

public sealed record PlacementConflictValues
{
    public required int PlacementId { get; init; }

    public required int LotId { get; init; }

    public required int ResourceId { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }

    public required IReadOnlyList<RuleViolationResponse> Violations { get; init; }
}

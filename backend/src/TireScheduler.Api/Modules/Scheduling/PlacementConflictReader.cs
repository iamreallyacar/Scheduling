using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class PlacementConflictReader(ScheduleVersions versions, ScheduleSnapshotLoader snapshots) : IPlacementConflictReader
{
    public async Task<IReadOnlyList<PlacementConflictValues>> FindConflictsAsync(string versionId, CancellationToken cancellationToken)
    {
        int resolvedVersionId = await versions.ResolveAsync(versionId, cancellationToken);
        ScheduleSnapshot snapshot = await snapshots.ForVersionAsync(resolvedVersionId, cancellationToken);
        Dictionary<int, SnapshotPlacement> placementsById = snapshot.Placements.ToDictionary(placement => placement.Id);

        // FindConflicts already lists them by planned start, then placement id.
        return
        [
            .. PlacementRules.FindConflicts(snapshot).Select(conflict =>
            {
                SnapshotPlacement placement = placementsById[conflict.PlacementId];
                return new PlacementConflictValues
                {
                    PlacementId = placement.Id,
                    LotId = placement.LotId,
                    ResourceId = placement.ResourceId,
                    PlannedStart = placement.PlannedStart,
                    PlannedEnd = placement.PlannedEnd,
                    Violations = [.. conflict.Violations.Select(RuleViolationProblems.ToResponse)],
                };
            }),
        ];
    }
}

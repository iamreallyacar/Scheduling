namespace TireScheduler.Api.Modules.Scheduling;

// The public read interface of Scheduling for Queries, which derives the statuses, the late flag
// and the order views from it (DEC-12). `live` names the live version (DEC-17); a version that
// does not exist is answered 404.
public interface IPlacementReader
{
    // Every lot placed in the version.
    Task<IReadOnlySet<int>> PlacedLotIdsAsync(string versionId, CancellationToken cancellationToken);

    // The placements of the lots among lotIds, by lot id; a lot without one is missing.
    Task<IReadOnlyDictionary<int, PlacementValues>> PlacementsOfLotsAsync(string versionId, IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);

    // The placements of the version that overlap the half-open range, by planned start, then id.
    Task<IReadOnlyList<PlacementValues>> PlacementsOverlappingAsync(string versionId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken);
}

public sealed record PlacementValues
{
    public required int Id { get; init; }

    public required int LotId { get; init; }

    public required int ResourceId { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }
}

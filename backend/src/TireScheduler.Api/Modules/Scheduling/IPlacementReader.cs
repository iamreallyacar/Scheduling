namespace TireScheduler.Api.Modules.Scheduling;

// The public read interface of Scheduling for Queries, which derives the lot status from it
// (DEC-12).
public interface IPlacementReader
{
    // The lots among lotIds that have a placement in the version. `live` names the live version
    // (DEC-17); a version that does not exist is answered 404.
    Task<IReadOnlySet<int>> PlacedLotIdsAsync(string versionId, IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);
}

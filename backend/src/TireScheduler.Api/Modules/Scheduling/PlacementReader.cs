using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.Scheduling;

// Scheduling stores no schedule versions or placements before task M3-4, so the live version is
// the only one and no lot is placed in it yet (A39).
internal sealed class PlacementReader : IPlacementReader
{
    private const string LiveVersionAlias = "live";

    public Task<IReadOnlySet<int>> PlacedLotIdsAsync(string versionId, IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        if (versionId != LiveVersionAlias)
        {
            throw ApiProblemException.NotFound("This schedule version");
        }

        return Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }
}

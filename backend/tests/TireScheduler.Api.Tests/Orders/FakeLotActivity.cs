using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Tests.Orders;

// Stands in for Scheduling and Progress behind the Orders ports (DEC-16) and the Scheduling read
// interface of Queries until those modules store placements and progress, so the rules that
// depend on them can be tested now. Every placed lot is placed in the live version.
internal sealed class FakeLotActivity : ILotPlacements, ILotProgress, IPlacementReader
{
    public HashSet<int> PlacedLotIds { get; } = [];

    public HashSet<int> StartedLotIds { get; } = [];

    public HashSet<int> CompletedLotIds { get; } = [];

    public Task<bool> AnyPlacedInLiveVersionAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(PlacedLotIds.Contains));

    public Task<IReadOnlySet<int>> PlacedLotIdsAsync(string versionId, IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        Assert.Equal("live", versionId);
        return Task.FromResult<IReadOnlySet<int>>(lotIds.Where(PlacedLotIds.Contains).ToHashSet());
    }

    public Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(StartedLotIds.Contains));

    public Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(lotId => StartedLotIds.Contains(lotId) && !CompletedLotIds.Contains(lotId)));
}

using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Tests.Orders;

// Stands in for Scheduling and Progress behind the Orders ports (DEC-16) until those modules
// store placements and progress, so the order rules that depend on them can be tested now.
internal sealed class FakeLotActivity : ILotPlacements, ILotProgress
{
    public HashSet<int> PlacedLotIds { get; } = [];

    public HashSet<int> StartedLotIds { get; } = [];

    public HashSet<int> CompletedLotIds { get; } = [];

    public Task<bool> AnyPlacedInLiveVersionAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(PlacedLotIds.Contains));

    public Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(StartedLotIds.Contains));

    public Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult(lotIds.Any(lotId => StartedLotIds.Contains(lotId) && !CompletedLotIds.Contains(lotId)));
}

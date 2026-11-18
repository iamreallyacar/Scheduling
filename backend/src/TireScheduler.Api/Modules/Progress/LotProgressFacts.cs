using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Modules.Progress;

// Answers the Orders port (DEC-16). Progress stores no actual starts before task M5-1, so no lot has started yet.
internal sealed class LotProgressFacts : ILotProgress
{
    public Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) => Task.FromResult(false);
}

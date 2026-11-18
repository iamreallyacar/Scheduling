using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Modules.Progress;

// Answers the ports of Orders and Scheduling (DEC-16). Progress stores no actual starts before
// task M5-1, so no lot has started yet.
internal sealed class LotProgressFacts : ILotProgress, ILotProgressStates
{
    public Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IReadOnlyDictionary<int, StartedLotState>> StartedLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, StartedLotState>>(new Dictionary<int, StartedLotState>());
}

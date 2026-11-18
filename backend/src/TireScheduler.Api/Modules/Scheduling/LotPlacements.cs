using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Modules.Scheduling;

// Answers the Orders port (DEC-16). Scheduling stores no placements before task M3-4, so no lot is placed yet.
internal sealed class LotPlacements : ILotPlacements
{
    public Task<bool> AnyPlacedInLiveVersionAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) => Task.FromResult(false);
}

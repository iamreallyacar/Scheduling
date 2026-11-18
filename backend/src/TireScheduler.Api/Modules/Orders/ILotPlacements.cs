namespace TireScheduler.Api.Modules.Orders;

// Port (DEC-16), implemented by Scheduling: whether lots are placed, which decides if an order
// may still change its product or quantity (AC-49) or be deleted (A13).
public interface ILotPlacements
{
    Task<bool> AnyPlacedInLiveVersionAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);
}

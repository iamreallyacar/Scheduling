namespace TireScheduler.Api.Modules.Orders;

// Port (DEC-16), implemented by Progress: whether lots have started, which decides if an order
// may still change (AC-49), be cancelled (AC-51) or be deleted (A13).
public interface ILotProgress
{
    // In Progress or Completed.
    Task<bool> AnyStartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);

    // Started and not completed.
    Task<bool> AnyInProgressAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);
}

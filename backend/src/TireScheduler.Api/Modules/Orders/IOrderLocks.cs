namespace TireScheduler.Api.Modules.Orders;

// Row locks of Orders for the commands of the modules above it (DEC-33). Each method joins the
// command's open transaction and holds the row until it ends.
public interface IOrderLocks
{
    // False when the order does not exist. A soft-deleted order is locked too; reading it then
    // finds nothing.
    Task<bool> LockOrderAsync(int orderId, CancellationToken cancellationToken);
}

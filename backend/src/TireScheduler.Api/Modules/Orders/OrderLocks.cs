using TireScheduler.Api.Database;

namespace TireScheduler.Api.Modules.Orders;

internal sealed class OrderLocks(OrdersDbContext context, RequestTransaction requestTransaction) : IOrderLocks
{
    public async Task<bool> LockOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        requestTransaction.Join(context);
        return await context.LockRowAsync<Order>(orderId, cancellationToken);
    }
}

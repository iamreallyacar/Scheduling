namespace TireScheduler.Api.Queries.Orders;

// The order status table of domain-model.md section 6, first matching row wins. Completed, In
// Progress and Scheduled need progress and placements, which tasks M5-1 and M3-4 add.
internal static class OrderStatusRules
{
    public static OrderStatus Derive(DateTimeOffset? cancelledAt) =>
        cancelledAt is null ? OrderStatus.Pending : OrderStatus.Cancelled;
}

namespace TireScheduler.Api.Queries.Orders;

// The order status table of domain-model.md section 6, first matching row wins. Completed and In
// Progress need progress, which task M5-1 adds.
internal static class OrderStatusRules
{
    public static OrderStatus Derive(DateTimeOffset? cancelledAt, bool isAnyLotPlacedInVersion) =>
        cancelledAt is not null ? OrderStatus.Cancelled
        : isAnyLotPlacedInVersion ? OrderStatus.Scheduled
        : OrderStatus.Pending;
}

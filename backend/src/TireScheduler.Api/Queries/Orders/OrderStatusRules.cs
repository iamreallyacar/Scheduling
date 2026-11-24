namespace TireScheduler.Api.Queries.Orders;

// The order status table of domain-model.md section 6, first matching row wins, from the derived
// status of every lot of the order in the version. Every order has a lot (AC-46).
internal static class OrderStatusRules
{
    public static OrderStatus Derive(DateTimeOffset? cancelledAt, IReadOnlyCollection<LotStatus> lotStatuses) =>
        cancelledAt is not null ? OrderStatus.Cancelled
        : lotStatuses.All(status => status == LotStatus.Completed) ? OrderStatus.Completed
        : lotStatuses.Any(status => status is LotStatus.InProgress or LotStatus.Completed) ? OrderStatus.InProgress
        : lotStatuses.Any(status => status == LotStatus.Scheduled) ? OrderStatus.Scheduled
        : OrderStatus.Pending;
}

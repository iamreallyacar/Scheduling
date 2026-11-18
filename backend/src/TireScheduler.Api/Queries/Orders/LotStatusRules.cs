namespace TireScheduler.Api.Queries.Orders;

// The lot status table of domain-model.md section 6, first matching row wins. Completed and In
// Progress need progress, which task M5-1 adds.
internal static class LotStatusRules
{
    public static LotStatus Derive(DateTimeOffset? orderCancelledAt, bool isPlacedInVersion) =>
        orderCancelledAt is not null ? LotStatus.Cancelled
        : isPlacedInVersion ? LotStatus.Scheduled
        : LotStatus.Unscheduled;
}

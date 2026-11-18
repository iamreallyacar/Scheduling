namespace TireScheduler.Api.Queries.Orders;

// The lot status table of domain-model.md section 6, first matching row wins. Completed, In
// Progress and Scheduled need progress and placements, which tasks M5-1 and M3-4 add.
internal static class LotStatusRules
{
    public static LotStatus Derive(DateTimeOffset? orderCancelledAt) =>
        orderCancelledAt is null ? LotStatus.Unscheduled : LotStatus.Cancelled;
}

using TireScheduler.Api.Modules.Progress;

namespace TireScheduler.Api.Queries.Orders;

// The lot status table of domain-model.md section 6, first matching row wins. actuals is the lot's
// progress, null before it has started.
internal static class LotStatusRules
{
    public static LotStatus Derive(DateTimeOffset? orderCancelledAt, LotActuals? actuals, bool isPlacedInVersion) =>
        orderCancelledAt is not null && actuals is null ? LotStatus.Cancelled
        : actuals is { ActualEnd: not null } ? LotStatus.Completed
        : actuals is not null ? LotStatus.InProgress
        : isPlacedInVersion ? LotStatus.Scheduled
        : LotStatus.Unscheduled;
}

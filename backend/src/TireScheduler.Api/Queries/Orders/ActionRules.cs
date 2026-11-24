using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Orders;

// The actions a lot's state allows (DEC-70). Placing and moving follow R8 of the engine; progress
// follows the two checks of a progress write: a live placement and an order that is not cancelled.
internal static class LotActionRules
{
    public static LotActionsResponse Derive(LotStatus status, bool orderIsCancelled, bool isPlacedLive) => new()
    {
        CanChangePlacement = LotRules.CanChangePlacement(ProgressOf(status), orderIsCancelled),
        CanRecordProgress = ProgressRules.CanRecordProgress(isPlacedLive, orderIsCancelled),
    };

    private static LotProgressState ProgressOf(LotStatus status) => status switch
    {
        LotStatus.Completed => LotProgressState.Completed,
        LotStatus.InProgress => LotProgressState.InProgress,
        _ => LotProgressState.NotStarted,
    };
}

// The order's cancelled state decides, as R8 rejects every lot of a cancelled order (AC-133).
internal static class OrderActionRules
{
    public static OrderActionsResponse Derive(DateTimeOffset? cancelledAt) => new() { CanScheduleAsap = cancelledAt is null };
}

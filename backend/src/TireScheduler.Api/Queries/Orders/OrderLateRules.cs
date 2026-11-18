using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Orders;

// The late flag of domain-model.md section 6 for one version, from the engine (DEC-30), for every
// read that shows it. Until task M5-1 no lot is completed, so there is no completion time.
internal static class OrderLateRules
{
    public static bool IsLate(OrderSummary order, IReadOnlyDictionary<int, PlacementValues> placementsByLot, string plantTimeZoneId, DateTimeOffset now) =>
        OrderLateness.IsLate(new LatenessFacts(
            order.DueDate,
            plantTimeZoneId,
            now,
            IsCancelled: order.CancelledAt is not null,
            PlannedEnds: [.. order.LotIds.Where(placementsByLot.ContainsKey).Select(lotId => placementsByLot[lotId].PlannedEnd)],
            CompletedAt: null));
}

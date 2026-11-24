using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Progress;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Orders;

// The late flag of domain-model.md section 6 for one version, from the engine (DEC-30, DEC-62), for
// every read that shows it.
internal static class OrderLateRules
{
    public static bool IsLate(
        OrderSummary order,
        IReadOnlyDictionary<int, PlacementValues> placementsByLot,
        IReadOnlyDictionary<int, LotActuals> actualsByLot,
        string plantTimeZoneId,
        DateTimeOffset now) =>
        OrderLateness.IsLate(new LatenessFacts(
            order.DueDate,
            plantTimeZoneId,
            now,
            IsCancelled: order.CancelledAt is not null,
            PlannedEnds: [.. order.LotIds.Where(placementsByLot.ContainsKey).Select(lotId => placementsByLot[lotId].PlannedEnd)],
            CompletedAt: OrderLateness.CompletionTime([.. order.LotIds.Select(lotId => actualsByLot.GetValueOrDefault(lotId)?.ActualEnd)])));
}

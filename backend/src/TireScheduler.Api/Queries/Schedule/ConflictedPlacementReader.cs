using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Queries.Schedule;

// The conflicted placements of a version (F-SCH-4), in the order Scheduling finds them (AC-183).
internal sealed class ConflictedPlacementReader(IPlacementConflictReader conflicts, IOrderReader orders, IMasterDataReader masterData)
{
    public async Task<IReadOnlyList<ConflictedPlacementResponse>> ListAsync(string versionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlacementConflictValues> placementConflicts = await conflicts.FindConflictsAsync(versionId, cancellationToken);
        if (placementConflicts.Count == 0)
        {
            return [];
        }

        // Every conflicted lot's order was found by FindConflicts, so each lot is found here.
        IReadOnlyDictionary<int, LotContext> lotsById = await orders.FindLotContextsAsync([.. placementConflicts.Select(conflict => conflict.LotId)], cancellationToken);
        // A resource with placements cannot be deleted (AC-20), so every placement finds its resource.
        IReadOnlyDictionary<int, NamedReference> resourcesById = await masterData.GetResourceNamesAsync(
            [.. placementConflicts.Select(conflict => conflict.ResourceId).Distinct()],
            cancellationToken);

        return
        [
            .. placementConflicts.Select(conflict =>
            {
                LotContext lotContext = lotsById[conflict.LotId];
                return new ConflictedPlacementResponse
                {
                    PlacementId = conflict.PlacementId,
                    PlannedStart = conflict.PlannedStart,
                    PlannedEnd = conflict.PlannedEnd,
                    Resource = resourcesById[conflict.ResourceId],
                    LotId = lotContext.Lot.Id,
                    LotNo = lotContext.Lot.LotNo,
                    Quantity = lotContext.Lot.Quantity,
                    DurationMinutes = lotContext.Lot.DurationMinutes,
                    OrderId = lotContext.Order.Id,
                    OrderNo = lotContext.Order.OrderNo,
                    SequenceNo = lotContext.Operation.SequenceNo,
                    ResourceType = lotContext.Operation.ResourceType,
                    Violations = conflict.Violations,
                };
            }),
        ];
    }
}

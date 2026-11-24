using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Progress;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries.Orders;

namespace TireScheduler.Api.Queries.Schedule;

// The conflicted placements of a version (F-SCH-4), in the order Scheduling finds them (AC-183).
internal sealed class ConflictedPlacementReader(IPlacementConflictReader conflicts, IOrderReader orders, ILotProgressReader progress, IMasterDataReader masterData)
{
    public async Task<IReadOnlyList<ConflictedPlacementResponse>> ListAsync(string versionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlacementConflictValues> placementConflicts = await conflicts.FindConflictsAsync(versionId, cancellationToken);
        if (placementConflicts.Count == 0)
        {
            return [];
        }

        // Every conflicted lot's order was found by FindConflicts, so each lot is found here.
        int[] conflictedLotIds = [.. placementConflicts.Select(conflict => conflict.LotId)];
        IReadOnlyDictionary<int, LotContext> lotsById = await orders.FindLotContextsAsync(conflictedLotIds, cancellationToken);
        // The status and actions of a started lot come from its progress (DEC-63, DEC-70).
        IReadOnlyDictionary<int, LotActuals> actuals = await progress.ActualsOfLotsAsync(conflictedLotIds, cancellationToken);
        // A resource with placements cannot be deleted (AC-20), so every placement finds its resource.
        IReadOnlyDictionary<int, NamedReference> resourcesById = await masterData.GetResourceNamesAsync(
            [.. placementConflicts.Select(conflict => conflict.ResourceId).Distinct()],
            cancellationToken);

        return
        [
            .. placementConflicts.Select(conflict =>
            {
                LotContext lotContext = lotsById[conflict.LotId];
                LotStatus status = LotStatusRules.Derive(lotContext.Order.CancelledAt, actuals.GetValueOrDefault(conflict.LotId), isPlacedInVersion: true);
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
                    Status = status,
                    Actions = LotActionRules.Derive(status, orderIsCancelled: lotContext.Order.CancelledAt is not null, isPlacedLive: true),
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

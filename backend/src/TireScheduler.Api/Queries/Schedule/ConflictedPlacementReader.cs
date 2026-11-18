using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
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
        Dictionary<int, (OrderDetails Order, OperationDetails Operation, LotDetails Lot)> lotsById = (await orders.ListAllDetailsAsync(cancellationToken))
            .SelectMany(order => order.Operations.SelectMany(operation => operation.Lots.Select(lot => (Order: order, Operation: operation, Lot: lot))))
            .ToDictionary(entry => entry.Lot.Id);
        // A resource with placements cannot be deleted (AC-20), so every placement finds its resource.
        Dictionary<int, NamedReference> resourcesById = (await masterData.ListResourcesAsync(resourceTypeId: null, isActive: null, cancellationToken))
            .ToDictionary(resource => resource.Id, resource => new NamedReference { Id = resource.Id, Name = resource.Name });

        return
        [
            .. placementConflicts.Select(conflict =>
            {
                (OrderDetails order, OperationDetails operation, LotDetails lot) = lotsById[conflict.LotId];
                return new ConflictedPlacementResponse
                {
                    PlacementId = conflict.PlacementId,
                    PlannedStart = conflict.PlannedStart,
                    PlannedEnd = conflict.PlannedEnd,
                    Resource = resourcesById[conflict.ResourceId],
                    LotId = lot.Id,
                    LotNo = lot.LotNo,
                    Quantity = lot.Quantity,
                    DurationMinutes = lot.DurationMinutes,
                    OrderId = order.Order.Id,
                    OrderNo = order.Order.OrderNo,
                    SequenceNo = operation.SequenceNo,
                    ResourceType = operation.ResourceType,
                    Violations = conflict.Violations,
                };
            }),
        ];
    }
}

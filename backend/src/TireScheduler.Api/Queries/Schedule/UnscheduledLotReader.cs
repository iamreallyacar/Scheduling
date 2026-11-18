using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries.Orders;

namespace TireScheduler.Api.Queries.Schedule;

// The unscheduled-lots queue (F-SCH-7): the lots whose derived status in the version is
// Unscheduled, in the order list's default sort, then by stage (AC-174).
internal sealed class UnscheduledLotReader(IOrderReader orders, IPlacementReader placements)
{
    public async Task<IReadOnlyList<UnscheduledLotResponse>> ListAsync(string versionId, CancellationToken cancellationToken)
    {
        // First, so a version that does not exist is answered before every order is read.
        IReadOnlySet<int> placedLotIds = await placements.PlacedLotIdsAsync(versionId, cancellationToken);
        IReadOnlyList<OrderDetails> allOrders = await orders.ListAllDetailsAsync(cancellationToken);
        return
        [
            .. allOrders.SelectMany(order => order.Operations.SelectMany(operation => operation.Lots
                .Where(lot => LotStatusRules.Derive(order.Order.CancelledAt, placedLotIds.Contains(lot.Id)) == LotStatus.Unscheduled)
                .Select(lot => ToResponse(order, operation, lot)))),
        ];
    }

    private static UnscheduledLotResponse ToResponse(OrderDetails order, OperationDetails operation, LotDetails lot) => new()
    {
        Id = lot.Id,
        LotNo = lot.LotNo,
        Quantity = lot.Quantity,
        DurationMinutes = lot.DurationMinutes,
        OrderId = order.Order.Id,
        OrderNo = order.Order.OrderNo,
        DueDate = order.Order.DueDate,
        SequenceNo = operation.SequenceNo,
        ResourceType = operation.ResourceType,
        Predecessors =
        [
            .. order.Dependencies
                .Where(dependency => dependency.SuccessorSequenceNo == operation.SequenceNo)
                .Select(dependency => new LotPredecessorResponse
                {
                    SequenceNo = dependency.PredecessorSequenceNo,
                    ResourceType = order.Operations.Single(predecessor => predecessor.SequenceNo == dependency.PredecessorSequenceNo).ResourceType,
                    MinLagMinutes = dependency.MinLagMinutes,
                }),
        ],
    };
}

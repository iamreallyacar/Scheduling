using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Orders;

// The order reads (DEC-29): what Orders stores, with the statuses, late flag and progress derived
// on every read. Until tasks M3-4 and M5-1 no lot is placed or completed, so there are no
// planned ends and no completion time to pass on.
internal sealed class OrderViewReader(IOrderReader orders, IMasterDataReader masterData, TimeProvider clock)
{
    public async Task<OrderResponse> GetAsync(int orderId, CancellationToken cancellationToken)
    {
        OrderDetails details = await orders.GetAsync(orderId, cancellationToken);
        OrderSummary order = details.Order;
        List<OperationResponse> operations = details.Operations.Select(operation => WithLotStatus(operation, order.CancelledAt)).ToList();
        return new OrderResponse
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            CustomerName = order.CustomerName,
            Product = order.Product,
            Quantity = order.Quantity,
            DueDate = order.DueDate,
            Priority = order.Priority,
            CreatedAt = order.CreatedAt,
            CancelledAt = order.CancelledAt,
            Status = OrderStatusRules.Derive(order.CancelledAt),
            IsLate = IsLate(order.DueDate, order.CancelledAt, clock.GetUtcNow()),
            Progress = OrderProgressRules.Measure(operations),
            Operations = operations,
            Dependencies = details.Dependencies,
        };
    }

    private static OperationResponse WithLotStatus(OperationDetails operation, DateTimeOffset? orderCancelledAt) => new()
    {
        Id = operation.Id,
        SequenceNo = operation.SequenceNo,
        ResourceType = operation.ResourceType,
        Quantity = operation.Quantity,
        SetupMinutes = operation.SetupMinutes,
        CycleMinutes = operation.CycleMinutes,
        UnitsPerCycle = operation.UnitsPerCycle,
        Lots = operation.Lots
            .Select(lot => new LotResponse
            {
                Id = lot.Id,
                LotNo = lot.LotNo,
                Quantity = lot.Quantity,
                DurationMinutes = lot.DurationMinutes,
                // The order reads take the live placements in task M3-4 (AC-71).
                Status = LotStatusRules.Derive(orderCancelledAt, isPlacedInVersion: false),
            })
            .ToList(),
    };

    // Without a filter on derived values only one page is read; with one, every order matching
    // the other filters is derived first (DEC-31).
    public async Task<OrderPageResponse> ListAsync(OrderListRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        if (!request.FiltersOnDerivedValues)
        {
            OrderSummaryPage orderPage = await orders.ListPageAsync(request.Criteria, request.Page, request.PageSize, cancellationToken);
            return new OrderPageResponse
            {
                Items = orderPage.Items.Select(order => Summarize(order, now)).ToList(),
                Page = orderPage.Page,
                PageSize = orderPage.PageSize,
                TotalCount = orderPage.TotalCount,
            };
        }

        IReadOnlyList<OrderSummary> candidates = await orders.ListAllAsync(request.Criteria, cancellationToken);
        List<OrderSummaryResponse> matching = candidates.Select(order => Summarize(order, now)).Where(request.Matches).ToList();
        return new OrderPageResponse
        {
            Items = matching.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = matching.Count,
        };
    }

    private OrderSummaryResponse Summarize(OrderSummary order, DateTimeOffset now) => new()
    {
        Id = order.Id,
        OrderNo = order.OrderNo,
        CustomerName = order.CustomerName,
        Product = order.Product,
        Quantity = order.Quantity,
        DueDate = order.DueDate,
        Priority = order.Priority,
        CreatedAt = order.CreatedAt,
        CancelledAt = order.CancelledAt,
        Status = OrderStatusRules.Derive(order.CancelledAt),
        IsLate = IsLate(order.DueDate, order.CancelledAt, now),
    };

    private bool IsLate(DateOnly dueDate, DateTimeOffset? cancelledAt, DateTimeOffset now) =>
        OrderLateness.IsLate(new LatenessFacts(
            dueDate,
            masterData.PlantTimeZoneId,
            now,
            IsCancelled: cancelledAt is not null,
            PlannedEnds: [],
            CompletedAt: null));
}

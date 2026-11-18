using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Orders;

// The order reads (DEC-29): what Orders stores, with the statuses, late flag and progress derived
// on every read from the placements of the live version. Until task M5-1 no lot is completed, so
// there is no completion time to pass on.
internal sealed class OrderViewReader(IOrderReader orders, IPlacementReader placements, IMasterDataReader masterData, TimeProvider clock)
{
    public async Task<OrderResponse> GetAsync(int orderId, CancellationToken cancellationToken)
    {
        OrderDetails details = await orders.GetAsync(orderId, cancellationToken);
        OrderSummary order = details.Order;
        IReadOnlyDictionary<int, PlacementValues> livePlacements = await LivePlacementsAsync(order.LotIds, cancellationToken);
        IReadOnlyDictionary<int, NamedReference> resources = await ResourcesAsync(livePlacements.Values, cancellationToken);
        List<OperationResponse> operations = details.Operations
            .Select(operation => WithLotStatus(operation, order.CancelledAt, livePlacements, resources))
            .ToList();
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
            Status = StatusOf(order, livePlacements),
            IsLate = IsLate(order, livePlacements, clock.GetUtcNow()),
            Progress = OrderProgressRules.Measure(operations),
            Operations = operations,
            Dependencies = details.Dependencies,
        };
    }

    private static OperationResponse WithLotStatus(
        OperationDetails operation,
        DateTimeOffset? orderCancelledAt,
        IReadOnlyDictionary<int, PlacementValues> livePlacements,
        IReadOnlyDictionary<int, NamedReference> resources) => new()
    {
        Id = operation.Id,
        SequenceNo = operation.SequenceNo,
        ResourceType = operation.ResourceType,
        Quantity = operation.Quantity,
        SetupMinutes = operation.SetupMinutes,
        CycleMinutes = operation.CycleMinutes,
        UnitsPerCycle = operation.UnitsPerCycle,
        Lots = operation.Lots
            .Select(lot =>
            {
                PlacementValues? placement = livePlacements.GetValueOrDefault(lot.Id);
                return new LotResponse
                {
                    Id = lot.Id,
                    LotNo = lot.LotNo,
                    Quantity = lot.Quantity,
                    DurationMinutes = lot.DurationMinutes,
                    Status = LotStatusRules.Derive(orderCancelledAt, isPlacedInVersion: placement is not null),
                    Placement = placement is null
                        ? null
                        : new LotPlacementView
                        {
                            Resource = resources[placement.ResourceId],
                            PlannedStart = placement.PlannedStart,
                            PlannedEnd = placement.PlannedEnd,
                        },
                };
            })
            .ToList(),
    };

    // Without a filter on derived values only one page is read; with one, every order matching
    // the other filters is derived first (DEC-31). Either way the placements are read once.
    public async Task<OrderPageResponse> ListAsync(OrderListRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        if (!request.FiltersOnDerivedValues)
        {
            OrderSummaryPage orderPage = await orders.ListPageAsync(request.Criteria, request.Page, request.PageSize, cancellationToken);
            return new OrderPageResponse
            {
                Items = await SummarizeAsync(orderPage.Items, now, cancellationToken),
                Page = orderPage.Page,
                PageSize = orderPage.PageSize,
                TotalCount = orderPage.TotalCount,
            };
        }

        IReadOnlyList<OrderSummary> candidates = await orders.ListAllAsync(request.Criteria, cancellationToken);
        List<OrderSummaryResponse> matching = (await SummarizeAsync(candidates, now, cancellationToken)).Where(request.Matches).ToList();
        return new OrderPageResponse
        {
            Items = matching.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = matching.Count,
        };
    }

    private async Task<List<OrderSummaryResponse>> SummarizeAsync(IReadOnlyList<OrderSummary> summaries, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, PlacementValues> livePlacements = await LivePlacementsAsync([.. summaries.SelectMany(order => order.LotIds)], cancellationToken);
        return summaries.Select(order => new OrderSummaryResponse
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
            Status = StatusOf(order, livePlacements),
            IsLate = IsLate(order, livePlacements, now),
        }).ToList();
    }

    private Task<IReadOnlyDictionary<int, PlacementValues>> LivePlacementsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        placements.PlacementsOfLotsAsync(ScheduleVersionAlias.Live, lotIds, cancellationToken);

    // A resource with placements cannot be deleted (AC-20), so every placement finds its resource.
    private async Task<IReadOnlyDictionary<int, NamedReference>> ResourcesAsync(IEnumerable<PlacementValues> placementValues, CancellationToken cancellationToken)
    {
        if (!placementValues.Any())
        {
            return new Dictionary<int, NamedReference>();
        }

        IReadOnlyList<ResourceResponse> allResources = await masterData.ListResourcesAsync(resourceTypeId: null, isActive: null, cancellationToken);
        return allResources.ToDictionary(resource => resource.Id, resource => new NamedReference { Id = resource.Id, Name = resource.Name });
    }

    private static OrderStatus StatusOf(OrderSummary order, IReadOnlyDictionary<int, PlacementValues> livePlacements) =>
        OrderStatusRules.Derive(order.CancelledAt, isAnyLotPlacedInVersion: order.LotIds.Any(livePlacements.ContainsKey));

    private bool IsLate(OrderSummary order, IReadOnlyDictionary<int, PlacementValues> livePlacements, DateTimeOffset now) =>
        OrderLateness.IsLate(new LatenessFacts(
            order.DueDate,
            masterData.PlantTimeZoneId,
            now,
            IsCancelled: order.CancelledAt is not null,
            PlannedEnds: [.. order.LotIds.Where(livePlacements.ContainsKey).Select(lotId => livePlacements[lotId].PlannedEnd)],
            CompletedAt: null));
}

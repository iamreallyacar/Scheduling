using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Progress;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Queries.Orders;

// The order reads (DEC-29): what Orders stores, with the statuses, late flag and progress derived
// on every read from the placements of the live version and the lots' progress.
internal sealed class OrderViewReader(IOrderReader orders, IPlacementReader placements, ILotProgressReader progress, IMasterDataReader masterData, TimeProvider clock)
{
    public async Task<OrderResponse> GetAsync(int orderId, CancellationToken cancellationToken)
    {
        OrderDetails details = await orders.GetAsync(orderId, cancellationToken);
        OrderSummary order = details.Order;
        IReadOnlyDictionary<int, PlacementValues> livePlacements = await LivePlacementsAsync(order.LotIds, cancellationToken);
        IReadOnlyDictionary<int, NamedReference> resources = await ResourcesAsync(livePlacements.Values, cancellationToken);
        IReadOnlyDictionary<int, LotActuals> actuals = await progress.ActualsOfLotsAsync(order.LotIds, cancellationToken);
        List<OperationResponse> operations = details.Operations
            .Select(operation => WithLotStatus(operation, order.CancelledAt, livePlacements, actuals, resources))
            .ToList();
        return OrderResponse.From(
            order,
            StatusOf(order, livePlacements, actuals),
            IsLate(order, livePlacements, actuals, clock.GetUtcNow()),
            OrderProgressRules.Measure(operations),
            OrderActionRules.Derive(order.CancelledAt),
            operations,
            details.Dependencies);
    }

    private static OperationResponse WithLotStatus(
        OperationDetails operation,
        DateTimeOffset? orderCancelledAt,
        IReadOnlyDictionary<int, PlacementValues> livePlacements,
        IReadOnlyDictionary<int, LotActuals> actuals,
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
                LotActuals? lotActuals = actuals.GetValueOrDefault(lot.Id);
                LotStatus status = LotStatusRules.Derive(orderCancelledAt, lotActuals, isPlacedInVersion: placement is not null);
                return new LotResponse
                {
                    Id = lot.Id,
                    LotNo = lot.LotNo,
                    Quantity = lot.Quantity,
                    DurationMinutes = lot.DurationMinutes,
                    Status = status,
                    Actions = LotActionRules.Derive(status, orderIsCancelled: orderCancelledAt is not null, isPlacedLive: placement is not null),
                    ActualStart = lotActuals?.ActualStart,
                    ActualEnd = lotActuals?.ActualEnd,
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
    // the other filters is derived first (DEC-31). Either way the placements and progress are read once.
    public async Task<OrderPageResponse> ListAsync(OrderListRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        if (!request.FiltersOnDerivedValues)
        {
            OrderSummaryPage orderPage = await orders.ListPageAsync(request.Criteria, request.Page, request.PageSize, cancellationToken);
            return new OrderPageResponse
            {
                Items = await SummarizeAsync(ScheduleVersionAlias.Live, orderPage.Items, now, cancellationToken),
                Page = orderPage.Page,
                PageSize = orderPage.PageSize,
                TotalCount = orderPage.TotalCount,
            };
        }

        IReadOnlyList<OrderSummary> candidates = await orders.ListAllAsync(request.Criteria, cancellationToken);
        List<OrderSummaryResponse> matching = (await SummarizeAsync(ScheduleVersionAlias.Live, candidates, now, cancellationToken)).Where(request.Matches).ToList();
        return new OrderPageResponse
        {
            Items = matching.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = matching.Count,
        };
    }

    // Answers for the placements of the given version, which need not be the live one.
    public async Task<List<OrderSummaryResponse>> SummarizeAsync(string versionId, IReadOnlyList<OrderSummary> summaries, DateTimeOffset now, CancellationToken cancellationToken)
    {
        int[] lotIds = [.. summaries.SelectMany(order => order.LotIds)];
        IReadOnlyDictionary<int, PlacementValues> versionPlacements = await placements.PlacementsOfLotsAsync(versionId, lotIds, cancellationToken);
        IReadOnlyDictionary<int, LotActuals> actuals = await progress.ActualsOfLotsAsync(lotIds, cancellationToken);
        return summaries
            .Select(order => OrderSummaryResponse.From(order, StatusOf(order, versionPlacements, actuals), IsLate(order, versionPlacements, actuals, now)))
            .ToList();
    }

    private Task<IReadOnlyDictionary<int, PlacementValues>> LivePlacementsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken) =>
        placements.PlacementsOfLotsAsync(ScheduleVersionAlias.Live, lotIds, cancellationToken);

    // A resource with placements cannot be deleted (AC-20), so every placement finds its resource.
    private Task<IReadOnlyDictionary<int, NamedReference>> ResourcesAsync(IEnumerable<PlacementValues> placementValues, CancellationToken cancellationToken) =>
        masterData.GetResourceNamesAsync([.. placementValues.Select(placement => placement.ResourceId).Distinct()], cancellationToken);

    private static OrderStatus StatusOf(OrderSummary order, IReadOnlyDictionary<int, PlacementValues> placements, IReadOnlyDictionary<int, LotActuals> actuals) =>
        OrderStatusRules.Derive(
            order.CancelledAt,
            [.. order.LotIds.Select(lotId => LotStatusRules.Derive(order.CancelledAt, actuals.GetValueOrDefault(lotId), placements.ContainsKey(lotId)))]);

    private bool IsLate(OrderSummary order, IReadOnlyDictionary<int, PlacementValues> placements, IReadOnlyDictionary<int, LotActuals> actuals, DateTimeOffset now) =>
        OrderLateRules.IsLate(order, placements, actuals, masterData.PlantTimeZoneId, now);
}

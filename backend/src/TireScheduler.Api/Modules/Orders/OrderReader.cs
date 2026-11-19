using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Orders;

internal sealed class OrderReader(OrdersDbContext context, IMasterDataReader masterData) : IOrderReader
{
    public async Task<OrderDetails> GetAsync(int orderId, CancellationToken cancellationToken)
    {
        Order order = await WithOperationsAndDependencies(context.Orders)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw ApiProblemException.NotFound("This order");

        return (await DetailAsync([order], cancellationToken))[0];
    }

    public async Task<OrderSummaryPage> ListPageAsync(OrderListCriteria criteria, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        IQueryable<Order> orders = await MatchingAsync(criteria, cancellationToken);
        int totalCount = await orders.CountAsync(cancellationToken);
        List<Order> pageOfOrders = await orders.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new OrderSummaryPage
        {
            Items = await SummarizeAsync(pageOfOrders, cancellationToken),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<OrderSummary>> ListAllAsync(OrderListCriteria criteria, CancellationToken cancellationToken)
    {
        IQueryable<Order> orders = await MatchingAsync(criteria, cancellationToken);
        return await SummarizeAsync(await orders.ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDetails>> ListAllDetailsAsync(CancellationToken cancellationToken)
    {
        List<Order> orders = await WithOperationsAndDependencies(Sorted(context.Orders, OrderSort.Default)).ToListAsync(cancellationToken);
        return await DetailAsync(orders, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, LotContext>> FindLotContextsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        List<Order> ordersOfLots = await WithOperationsAndDependencies(context.Orders)
            .Where(o => o.Operations.Any(operation => operation.Lots.Any(lot => lotIds.Contains(lot.Id))))
            .ToListAsync(cancellationToken);
        // The orders come whole, so their other lots are left out of the answer here.
        HashSet<int> wantedLotIds = [.. lotIds];
        return (await DetailAsync(ordersOfLots, cancellationToken))
            .SelectMany(order => order.Operations.SelectMany(operation => operation.Lots
                .Where(lot => wantedLotIds.Contains(lot.Id))
                .Select(lot => new LotContext { Order = order.Order, Operation = operation, Lot = lot })))
            .ToDictionary(lotContext => lotContext.Lot.Id);
    }

    private static IQueryable<Order> WithOperationsAndDependencies(IQueryable<Order> orders) =>
        orders
            .Include(o => o.Operations).ThenInclude(operation => operation.Lots)
            .Include(o => o.Dependencies)
            .AsSplitQuery()
            .AsNoTracking();

    private async Task<IReadOnlyList<OrderDetails>> DetailAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrderSummary> summaries = await SummarizeAsync(orders, cancellationToken);
        int[] resourceTypeIds = orders.SelectMany(order => order.Operations).Select(operation => operation.ResourceTypeId).Distinct().ToArray();
        IReadOnlyDictionary<int, NamedReference> resourceTypes = await masterData.GetResourceTypesAsync(resourceTypeIds, cancellationToken);
        return orders.Select((order, index) => ToDetails(order, summaries[index], resourceTypes)).ToList();
    }

    private static OrderDetails ToDetails(Order order, OrderSummary summary, IReadOnlyDictionary<int, NamedReference> resourceTypes)
    {
        Dictionary<int, int> sequenceNoByOperationId = order.Operations.ToDictionary(operation => operation.Id, operation => operation.SequenceNo);
        return new OrderDetails
        {
            Order = summary,
            Operations = order.Operations
                .OrderBy(operation => operation.SequenceNo)
                .Select(operation => ToDetails(
                    operation,
                    resourceTypes.GetValueOrDefault(operation.ResourceTypeId)
                        ?? throw new InvalidOperationException($"Operation {operation.Id} needs resource type {operation.ResourceTypeId}, which does not exist.")))
                .ToList(),
            Dependencies = order.Dependencies
                .Select(dependency => new OperationDependencyResponse
                {
                    PredecessorSequenceNo = sequenceNoByOperationId[dependency.PredecessorOperationId],
                    SuccessorSequenceNo = sequenceNoByOperationId[dependency.SuccessorOperationId],
                    MinLagMinutes = dependency.MinLagMinutes,
                })
                .OrderBy(dependency => dependency.SuccessorSequenceNo)
                .ThenBy(dependency => dependency.PredecessorSequenceNo)
                .ToList(),
        };
    }

    private async Task<IQueryable<Order>> MatchingAsync(OrderListCriteria criteria, CancellationToken cancellationToken)
    {
        IQueryable<Order> orders = context.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            string searchText = criteria.SearchText.Trim();
            string pattern = ContainsPattern.For(searchText);
            IReadOnlyList<int> productIds = await masterData.FindProductIdsContainingAsync(searchText, cancellationToken);
            orders = orders.Where(order => EF.Functions.ILike(order.OrderNo, pattern, ContainsPattern.EscapeCharacter)
                || EF.Functions.ILike(order.CustomerName, pattern, ContainsPattern.EscapeCharacter)
                || productIds.Contains(order.ProductId));
        }

        if (!string.IsNullOrWhiteSpace(criteria.CustomerText))
        {
            string pattern = ContainsPattern.For(criteria.CustomerText.Trim());
            orders = orders.Where(order => EF.Functions.ILike(order.CustomerName, pattern, ContainsPattern.EscapeCharacter));
        }

        if (criteria.DueFrom is DateOnly dueFrom)
        {
            orders = orders.Where(order => order.DueDate >= dueFrom);
        }

        if (criteria.DueTo is DateOnly dueTo)
        {
            orders = orders.Where(order => order.DueDate <= dueTo);
        }

        if (criteria.Priorities.Count > 0)
        {
            OrderPriority[] priorities = criteria.Priorities.ToArray();
            orders = orders.Where(order => priorities.Contains(order.Priority));
        }

        return Sorted(orders, criteria.Sort);
    }

    private static IQueryable<Order> Sorted(IQueryable<Order> orders, OrderSort sort)
    {
        IOrderedQueryable<Order> sorted = sort.Field switch
        {
            OrderSortField.OrderNo => SortBy(orders, order => order.OrderNo, sort.Descending),
            OrderSortField.CustomerName => SortBy(orders, order => order.CustomerName, sort.Descending),
            // Stored as text, so the rank is spelled out: Low before Normal before High.
            OrderSortField.Priority => SortBy(
                orders,
                order => order.Priority == OrderPriority.High ? 2 : order.Priority == OrderPriority.Normal ? 1 : 0,
                sort.Descending),
            OrderSortField.DueDate => SortBy(orders, order => order.DueDate, sort.Descending),
            OrderSortField.CreatedAt => SortBy(orders, order => order.CreatedAt, sort.Descending),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, "Unknown order sort field."),
        };
        return sort.Field == OrderSortField.OrderNo ? sorted : sorted.ThenBy(order => order.OrderNo);
    }

    private static IOrderedQueryable<Order> SortBy<TKey>(IQueryable<Order> orders, Expression<Func<Order, TKey>> key, bool descending) =>
        descending ? orders.OrderByDescending(key) : orders.OrderBy(key);

    public async Task<int?> FindOrderIdOfLotAsync(int lotId, CancellationToken cancellationToken) =>
        await context.Orders
            .SelectMany(order => order.Operations.SelectMany(operation => operation.Lots), (order, lot) => new { OrderId = order.Id, LotId = lot.Id })
            .Where(pair => pair.LotId == lotId)
            .Select(pair => (int?)pair.OrderId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<OrderSchedulingFacts?> FindSchedulingFactsAsync(int orderId, CancellationToken cancellationToken)
    {
        Order? order = await WithOperationsAndDependencies(context.Orders).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        return order is null ? null : ToSchedulingFacts(order);
    }

    public async Task<IReadOnlyList<OrderSchedulingFacts>> ListSchedulingFactsOfLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        List<Order> orders = await WithOperationsAndDependencies(context.Orders)
            .Where(o => o.Operations.Any(operation => operation.Lots.Any(lot => lotIds.Contains(lot.Id))))
            .ToListAsync(cancellationToken);
        return orders.Select(ToSchedulingFacts).ToList();
    }

    private static OrderSchedulingFacts ToSchedulingFacts(Order order) => new()
    {
        IsCancelled = order.CancelledAt is not null,
        Lots = order.Operations
            .SelectMany(operation => operation.Lots.Select(lot => new SchedulingLotFacts
            {
                Id = lot.Id,
                OperationId = operation.Id,
                ResourceTypeId = operation.ResourceTypeId,
                DurationMinutes = lot.DurationMinutes,
            }))
            .ToList(),
        Dependencies = order.Dependencies
            .Select(dependency => new SchedulingDependencyFacts
            {
                PredecessorOperationId = dependency.PredecessorOperationId,
                SuccessorOperationId = dependency.SuccessorOperationId,
                MinLagMinutes = dependency.MinLagMinutes,
            })
            .ToList(),
    };

    private async Task<IReadOnlyList<OrderSummary>> SummarizeAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, ProductReference> products = await masterData
            .GetProductsAsync(orders.Select(order => order.ProductId).Distinct().ToArray(), cancellationToken);
        int[] orderIds = orders.Select(order => order.Id).ToArray();
        ILookup<int, int> lotIdsByOrder = (await context.Operations
                .Where(operation => orderIds.Contains(operation.OrderId))
                .SelectMany(operation => operation.Lots, (operation, lot) => new { operation.OrderId, LotId = lot.Id })
                .ToListAsync(cancellationToken))
            .ToLookup(pair => pair.OrderId, pair => pair.LotId);
        return orders
            .Select(order => ToSummary(
                order,
                products.GetValueOrDefault(order.ProductId) ?? throw MissingProduct(order.Id, order.ProductId),
                [.. lotIdsByOrder[order.Id].Order()]))
            .ToList();
    }

    private static OrderSummary ToSummary(Order order, ProductReference product, IReadOnlyList<int> lotIds) => new()
    {
        Id = order.Id,
        OrderNo = order.OrderNo,
        CustomerName = order.CustomerName,
        Product = product,
        Quantity = order.Quantity,
        DueDate = order.DueDate,
        Priority = order.Priority,
        CreatedAt = order.CreatedAt,
        CancelledAt = order.CancelledAt,
        LotIds = lotIds,
    };

    private static OperationDetails ToDetails(Operation operation, NamedReference resourceType) => new()
    {
        Id = operation.Id,
        SequenceNo = operation.SequenceNo,
        ResourceType = resourceType,
        Quantity = operation.Quantity,
        SetupMinutes = operation.SetupMinutes,
        CycleMinutes = operation.CycleMinutes,
        UnitsPerCycle = operation.UnitsPerCycle,
        Lots = operation.Lots
            .OrderBy(lot => lot.LotNo)
            .Select(lot => new LotDetails
            {
                Id = lot.Id,
                LotNo = lot.LotNo,
                Quantity = lot.Quantity,
                DurationMinutes = lot.DurationMinutes,
            })
            .ToList(),
    };

    // A product used by an order cannot be deleted (AC-20), so this means a bug.
    private static InvalidOperationException MissingProduct(int orderId, int productId) =>
        new($"Order {orderId} points to product {productId}, which does not exist.");
}

namespace TireScheduler.Api.Modules.Orders;

// The public read interface of Orders for Queries, which serves the order reads (DEC-29): the
// order fields, operations and lots, without the derived status. Soft-deleted orders are never
// seen (query filter).
public interface IOrderReader
{
    // A missing order is answered 404.
    Task<OrderDetails> GetAsync(int orderId, CancellationToken cancellationToken);

    // Every order with its operations, lots and dependencies, in the default sort of the order
    // list (AC-68), for the views derived from every lot (A39).
    Task<IReadOnlyList<OrderDetails>> ListAllDetailsAsync(CancellationToken cancellationToken);

    // One page of the orders matching the criteria; page and pageSize are 1 or more.
    Task<OrderSummaryPage> ListPageAsync(OrderListCriteria criteria, int page, int pageSize, CancellationToken cancellationToken);

    // Every order matching the criteria, for filters on derived values (DEC-31).
    Task<IReadOnlyList<OrderSummary>> ListAllAsync(OrderListCriteria criteria, CancellationToken cancellationToken);

    // Null when the lot does not exist or its order is deleted.
    Task<int?> FindOrderIdOfLotAsync(int lotId, CancellationToken cancellationToken);

    // What the scheduling rules need of an order (R4, R8); null when it does not exist or is deleted.
    Task<OrderSchedulingFacts?> FindSchedulingFactsAsync(int orderId, CancellationToken cancellationToken);
}

public sealed record OrderSchedulingFacts
{
    public required bool IsCancelled { get; init; }

    public required IReadOnlyList<SchedulingLotFacts> Lots { get; init; }

    public required IReadOnlyList<SchedulingDependencyFacts> Dependencies { get; init; }
}

public sealed record SchedulingLotFacts
{
    public required int Id { get; init; }

    public required int OperationId { get; init; }

    public required int ResourceTypeId { get; init; }

    public required int DurationMinutes { get; init; }
}

public sealed record SchedulingDependencyFacts
{
    public required int PredecessorOperationId { get; init; }

    public required int SuccessorOperationId { get; init; }

    public required int MinLagMinutes { get; init; }
}

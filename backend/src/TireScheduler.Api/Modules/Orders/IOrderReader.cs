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
}

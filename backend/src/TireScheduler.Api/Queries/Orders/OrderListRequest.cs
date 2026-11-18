using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Queries.Orders;

// A checked order list request: what Orders filters in the database, and the filters on the
// derived status and late flag that only Queries can apply (DEC-31). Empty statuses and a null
// late flag mean no filter.
internal sealed record OrderListRequest
{
    public required OrderListCriteria Criteria { get; init; }

    public required IReadOnlySet<OrderStatus> Statuses { get; init; }

    public required bool? Late { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public bool FiltersOnDerivedValues => Statuses.Count > 0 || Late is not null;

    public bool Matches(OrderSummaryResponse order) =>
        (Statuses.Count == 0 || Statuses.Contains(order.Status)) && (Late is null || Late == order.IsLate);
}

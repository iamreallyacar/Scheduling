namespace TireScheduler.Api.Modules.Orders;

// What the order list filters and sorts on among the stored order fields (DEC-31). Texts are
// matched as A29 describes; null or empty means no filter.
public sealed record OrderListCriteria
{
    public required string? SearchText { get; init; }

    public required string? CustomerText { get; init; }

    public required DateOnly? DueFrom { get; init; }

    public required DateOnly? DueTo { get; init; }

    public required IReadOnlySet<OrderPriority> Priorities { get; init; }

    public required OrderSort Sort { get; init; }
}

// Orders with the same value follow the order number (AC-68).
public sealed record OrderSort
{
    public required OrderSortField Field { get; init; }

    public required bool Descending { get; init; }

    public static OrderSort Default { get; } = new() { Field = OrderSortField.DueDate, Descending = false };
}

public enum OrderSortField
{
    OrderNo,
    CustomerName,
    Priority,
    DueDate,
    CreatedAt,
}

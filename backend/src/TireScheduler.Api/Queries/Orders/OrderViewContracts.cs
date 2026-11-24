using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Queries.Orders;

// An order as the Orders module reads it, with its derived status, late flag, progress and lot
// status for the live version (DEC-29). Named required properties, as in OrderContracts.cs.
public sealed record OrderResponse
{
    public required int Id { get; init; }

    public required string OrderNo { get; init; }

    public required string CustomerName { get; init; }

    public required ProductReference Product { get; init; }

    public required int Quantity { get; init; }

    public required DateOnly DueDate { get; init; }

    public required OrderPriority Priority { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset? CancelledAt { get; init; }

    public required OrderStatus Status { get; init; }

    public required bool IsLate { get; init; }

    public required OrderProgressResponse Progress { get; init; }

    public required OrderActionsResponse Actions { get; init; }

    public required IReadOnlyList<OperationResponse> Operations { get; init; }

    public required IReadOnlyList<OperationDependencyResponse> Dependencies { get; init; }

    internal static OrderResponse From(
        OrderSummary order,
        OrderStatus status,
        bool isLate,
        OrderProgressResponse progress,
        OrderActionsResponse actions,
        IReadOnlyList<OperationResponse> operations,
        IReadOnlyList<OperationDependencyResponse> dependencies) => new()
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
        Status = status,
        IsLate = isLate,
        Progress = progress,
        Actions = actions,
        Operations = operations,
        Dependencies = dependencies,
    };
}

// What the order's state allows, whatever the role (DEC-70); the server still rejects the rest (R7).
public sealed record OrderActionsResponse
{
    public required bool CanScheduleAsap { get; init; }
}

// What a lot's state allows, whatever the role (DEC-70): place or move it, and record its progress.
public sealed record LotActionsResponse
{
    public required bool CanChangePlacement { get; init; }

    public required bool CanRecordProgress { get; init; }
}

public sealed record OrderProgressResponse
{
    public required int CompletedQuantity { get; init; }

    public required int TotalQuantity { get; init; }

    // Whole percent, rounded down (DEC-64).
    public required int PercentComplete { get; init; }
}

public sealed record OperationResponse
{
    public required int Id { get; init; }

    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required int Quantity { get; init; }

    public required int SetupMinutes { get; init; }

    public required int CycleMinutes { get; init; }

    public required int UnitsPerCycle { get; init; }

    public required IReadOnlyList<LotResponse> Lots { get; init; }
}

public sealed record LotResponse
{
    public required int Id { get; init; }

    public required int LotNo { get; init; }

    public required int Quantity { get; init; }

    public required int DurationMinutes { get; init; }

    public required LotStatus Status { get; init; }

    public required LotActionsResponse Actions { get; init; }

    // In the live version; null while the lot is not placed (AC-71).
    public required LotPlacementView? Placement { get; init; }

    // Null before the lot has started, and the end until it has completed (AC-72).
    public required DateTimeOffset? ActualStart { get; init; }

    public required DateTimeOffset? ActualEnd { get; init; }
}

public sealed record LotPlacementView
{
    public required NamedReference Resource { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }
}

public sealed record OrderSummaryResponse
{
    public required int Id { get; init; }

    public required string OrderNo { get; init; }

    public required string CustomerName { get; init; }

    public required ProductReference Product { get; init; }

    public required int Quantity { get; init; }

    public required DateOnly DueDate { get; init; }

    public required OrderPriority Priority { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset? CancelledAt { get; init; }

    public required OrderStatus Status { get; init; }

    public required bool IsLate { get; init; }

    internal static OrderSummaryResponse From(OrderSummary order, OrderStatus status, bool isLate) => new()
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
        Status = status,
        IsLate = isLate,
    };
}

public sealed record OrderPageResponse
{
    public required IReadOnlyList<OrderSummaryResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}

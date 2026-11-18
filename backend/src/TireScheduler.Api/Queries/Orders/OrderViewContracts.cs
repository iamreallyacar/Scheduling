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

    public required IReadOnlyList<OperationResponse> Operations { get; init; }

    public required IReadOnlyList<OperationDependencyResponse> Dependencies { get; init; }
}

public sealed record OrderProgressResponse
{
    public required int CompletedQuantity { get; init; }

    public required int TotalQuantity { get; init; }
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
}

public sealed record OrderPageResponse
{
    public required IReadOnlyList<OrderSummaryResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}

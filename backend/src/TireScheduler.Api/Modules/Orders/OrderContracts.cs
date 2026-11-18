using System.ComponentModel.DataAnnotations;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Orders;

// Create and change take the same fields (AC-45, AC-47, AC-48).
public sealed record OrderRequest
{
    [Required]
    [StringLength(Order.OrderNoMaxLength)]
    public string OrderNo { get; init; } = string.Empty;

    [Required]
    [StringLength(Order.CustomerNameMaxLength)]
    public string CustomerName { get; init; } = string.Empty;

    [Required]
    public int? ProductId { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? Quantity { get; init; }

    [Required]
    public DateOnly? DueDate { get; init; }

    // Normal when left out (A12).
    public OrderPriority? Priority { get; init; }
}

// The Orders module's view of an order for Queries, which adds the derived values (DEC-29).
// Named required properties: a field left out or two values of the same type swapped do not compile.
public sealed record OrderSummary
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

    // Of every operation, for the status and late flag that Queries derives from their placements.
    public required IReadOnlyList<int> LotIds { get; init; }
}

public sealed record OrderDetails
{
    public required OrderSummary Order { get; init; }

    public required IReadOnlyList<OperationDetails> Operations { get; init; }

    public required IReadOnlyList<OperationDependencyResponse> Dependencies { get; init; }
}

public sealed record OperationDetails
{
    public required int Id { get; init; }

    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required int Quantity { get; init; }

    public required int SetupMinutes { get; init; }

    public required int CycleMinutes { get; init; }

    public required int UnitsPerCycle { get; init; }

    public required IReadOnlyList<LotDetails> Lots { get; init; }
}

public sealed record LotDetails
{
    public required int Id { get; init; }

    public required int LotNo { get; init; }

    public required int Quantity { get; init; }

    public required int DurationMinutes { get; init; }
}

// Names the operations by sequence number, as the routing does (A24).
public sealed record OperationDependencyResponse
{
    public required int PredecessorSequenceNo { get; init; }

    public required int SuccessorSequenceNo { get; init; }

    public required int MinLagMinutes { get; init; }
}

public sealed record OrderSummaryPage
{
    public required IReadOnlyList<OrderSummary> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}

// Writes answer without the order, which Queries serves (DEC-29).
public sealed record OrderCreatedResponse
{
    public required int Id { get; init; }
}

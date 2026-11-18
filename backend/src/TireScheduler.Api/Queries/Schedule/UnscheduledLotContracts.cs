using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Queries.Schedule;

// A lot that waits for placement in a schedule version, with what the queue shows of its order
// and operation (AC-174). Flat like the order answers (DEC-34).
public sealed record UnscheduledLotResponse
{
    public required int Id { get; init; }

    public required int LotNo { get; init; }

    public required int Quantity { get; init; }

    public required int DurationMinutes { get; init; }

    public required int OrderId { get; init; }

    public required string OrderNo { get; init; }

    public required DateOnly DueDate { get; init; }

    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required IReadOnlyList<LotPredecessorResponse> Predecessors { get; init; }
}

// An operation of the same order that the lot's operation depends on.
public sealed record LotPredecessorResponse
{
    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required int MinLagMinutes { get; init; }
}

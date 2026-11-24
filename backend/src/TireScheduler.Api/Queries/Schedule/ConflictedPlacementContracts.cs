using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries.Orders;

namespace TireScheduler.Api.Queries.Schedule;

// A placement that breaks a rule after a later fact, with what the schedule page shows of its
// lot, order and machine (AC-183). Flat like the order answers (DEC-34).
public sealed record ConflictedPlacementResponse
{
    public required int PlacementId { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }

    public required NamedReference Resource { get; init; }

    public required int LotId { get; init; }

    public required int LotNo { get; init; }

    public required int Quantity { get; init; }

    public required int DurationMinutes { get; init; }

    // The lot's derived status in the version (DEC-63).
    public required LotStatus Status { get; init; }

    public required LotActionsResponse Actions { get; init; }

    public required int OrderId { get; init; }

    public required string OrderNo { get; init; }

    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required IReadOnlyList<RuleViolationResponse> Violations { get; init; }
}

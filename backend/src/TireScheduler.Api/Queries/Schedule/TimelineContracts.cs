using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries.Machines;
using TireScheduler.Api.Queries.Orders;

namespace TireScheduler.Api.Queries.Schedule;

// The timeline of a plant-local date range (AC-185, DEC-45): Start and End are the UTC instants
// at which the first day begins and the last day ends.
public sealed record TimelineResponse
{
    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset End { get; init; }

    public required IReadOnlyList<TimelineResourceTypeResponse> ResourceTypes { get; init; }
}

public sealed record TimelineResourceTypeResponse
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<TimelineResourceResponse> Resources { get; init; }
}

// A machine row with what overlaps the range, times as stored.
public sealed record TimelineResourceResponse
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required bool IsActive { get; init; }

    public required ResourceStatus Status { get; init; }

    public required IReadOnlyList<TimePeriodResponse> WorkingPeriods { get; init; }

    public required IReadOnlyList<UnavailabilityResponse> Unavailabilities { get; init; }

    public required IReadOnlyList<TimelinePlacementResponse> Placements { get; init; }
}

// Half-open: the end is not part of the period.
public sealed record TimePeriodResponse
{
    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset End { get; init; }
}

// A bar with what its popover shows of the lot, order and operation (AC-190). Flat like the
// order answers (DEC-34).
public sealed record TimelinePlacementResponse
{
    public required int PlacementId { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }

    public required int LotId { get; init; }

    public required int LotNo { get; init; }

    public required int Quantity { get; init; }

    public required int DurationMinutes { get; init; }

    public required LotStatus Status { get; init; }

    public required int OrderId { get; init; }

    public required string OrderNo { get; init; }

    public required ProductReference Product { get; init; }

    public required bool OrderIsLate { get; init; }

    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.Scheduling;

// Places or moves a lot (API.md section "Scheduling calls"). The server computes the end.
public sealed record LotPlacementRequest
{
    [Required]
    public int? ResourceId { get; init; }

    [Required]
    public DateTimeOffset? PlannedStart { get; init; }
}

// Times in UTC (API.md section "Conventions").
public sealed record LotPlacementResponse
{
    public required int LotId { get; init; }

    public required int ResourceId { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }
}

// One broken rule with its text (API.md section "Error format", DEC-42).
public sealed record RuleViolationResponse
{
    public required string Rule { get; init; }

    public required string Code { get; init; }

    public required ConflictResponse Conflict { get; init; }

    public required string Detail { get; init; }

    public required DateTimeOffset? EarliestAllowedStart { get; init; }
}

// The item a rule conflicts with: `placement`, `unavailability`, `lot` or `resource`.
public sealed record ConflictResponse
{
    public required string Type { get; init; }

    public required int Id { get; init; }
}

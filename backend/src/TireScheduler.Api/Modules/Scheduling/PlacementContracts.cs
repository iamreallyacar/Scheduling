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

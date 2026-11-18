using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.Scheduling;

public sealed record UnavailabilityRequest
{
    // An unknown kind is an unreadable value (AC-167).
    [Required]
    public UnavailabilityKind? Kind { get; init; }

    [Required]
    public DateTimeOffset? Start { get; init; }

    // Empty for a breakdown with no known end.
    public DateTimeOffset? End { get; init; }

    [StringLength(Unavailability.ReasonMaxLength)]
    public string? Reason { get; init; }
}

// Times in UTC (API.md section "Conventions").
public sealed record UnavailabilityResponse
{
    public required int Id { get; init; }

    public required int ResourceId { get; init; }

    public required UnavailabilityKind Kind { get; init; }

    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset? End { get; init; }

    public required string? Reason { get; init; }
}

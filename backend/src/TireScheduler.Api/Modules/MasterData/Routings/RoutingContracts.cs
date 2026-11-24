using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.MasterData.Routings;

// A routing is read and saved as a whole (AC-34). A dependency names its stages by sequence
// number, because stage ids do not survive a save (A24).
public sealed record RoutingRequest
{
    [Required]
    [MaxLength(RoutingRules.MaxStageCount, ErrorMessage = "A routing has at most {1} stages.")]
    public IReadOnlyList<RoutingStageRequest>? Stages { get; init; }

    [Required]
    public IReadOnlyList<RoutingDependencyRequest>? Dependencies { get; init; }
}

public sealed record RoutingStageRequest
{
    [Required]
    public int? SequenceNo { get; init; }

    [Required]
    public int? ResourceTypeId { get; init; }

    [Range(0, int.MaxValue)]
    public int? SetupMinutes { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CycleMinutes { get; init; }

    [Range(1, int.MaxValue)]
    public int? UnitsPerCycle { get; init; }
}

// The lag has no range annotation: a negative lag is answered as `routing.invalid_dependency`
// with the other dependency faults (AC-36), not as a field error.
public sealed record RoutingDependencyRequest
{
    [Required]
    public int? PredecessorSequenceNo { get; init; }

    [Required]
    public int? SuccessorSequenceNo { get; init; }

    public int? MinLagMinutes { get; init; }
}

public sealed record RoutingResponse
{
    public required IReadOnlyList<RoutingStageResponse> Stages { get; init; }

    public required IReadOnlyList<RoutingDependencyResponse> Dependencies { get; init; }
}

public sealed record RoutingStageResponse
{
    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required int SetupMinutes { get; init; }

    public required int CycleMinutes { get; init; }

    public required int UnitsPerCycle { get; init; }
}

public sealed record RoutingDependencyResponse
{
    public required int PredecessorSequenceNo { get; init; }

    public required int SuccessorSequenceNo { get; init; }

    public required int MinLagMinutes { get; init; }
}

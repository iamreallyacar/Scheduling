using System.ComponentModel.DataAnnotations;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Scheduling;

// A whole-order proposal (DEC-60): nothing is saved. Every lot of the order is listed, placed or
// proposed, in stage then lot order, so the review needs no second read. Times in UTC.
public sealed record OrderProposalResponse
{
    public required int OrderId { get; init; }

    // The start the search used, which the commit sends back.
    public required DateTimeOffset NotBefore { get; init; }

    public required IReadOnlyList<ProposalLotResponse> Lots { get; init; }

    // The latest planned end of the lots, and the late flag of the order once the proposal is saved (WQ2).
    public required DateTimeOffset FinishesAt { get; init; }

    public required bool WouldBeLate { get; init; }
}

public sealed record ProposalLotResponse
{
    public required int LotId { get; init; }

    public required int LotNo { get; init; }

    public required int SequenceNo { get; init; }

    public required NamedReference ResourceType { get; init; }

    public required NamedReference Resource { get; init; }

    public required DateTimeOffset PlannedStart { get; init; }

    public required DateTimeOffset PlannedEnd { get; init; }

    // False for a lot placed already, which the commit leaves where it is (AC-130).
    public required bool IsProposed { get; init; }
}

// The proposal sent back unchanged: its start and its proposed lots (AC-200).
public sealed record OrderProposalCommitRequest
{
    [Required]
    public DateTimeOffset? NotBefore { get; init; }

    [Required]
    public IReadOnlyList<ProposedPlacementRequest>? Placements { get; init; }
}

public sealed record ProposedPlacementRequest
{
    [Required]
    public int? LotId { get; init; }

    [Required]
    public int? ResourceId { get; init; }

    [Required]
    public DateTimeOffset? PlannedStart { get; init; }
}

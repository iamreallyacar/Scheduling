using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.Progress;

// Records or corrects the progress of a lot as a whole (A47). Times in UTC.
public sealed record LotProgressRequest
{
    [Required]
    public DateTimeOffset? ActualStart { get; init; }

    // Null while the lot runs.
    public DateTimeOffset? ActualEnd { get; init; }
}

public sealed record LotProgressResponse
{
    public required int LotId { get; init; }

    public required DateTimeOffset ActualStart { get; init; }

    public required DateTimeOffset? ActualEnd { get; init; }
}

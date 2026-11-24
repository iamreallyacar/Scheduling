namespace TireScheduler.Api.Modules.Progress;

// The public read interface of Progress for Queries, which derives the lot and order statuses, the
// late flag and the Running machine status from it (DEC-12, DEC-62).
public interface ILotProgressReader
{
    // The progress of the lots among lotIds, by lot id; a lot that has not started is missing.
    Task<IReadOnlyDictionary<int, LotActuals>> ActualsOfLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);

    // Every lot that has started and not completed.
    Task<IReadOnlyList<int>> InProgressLotIdsAsync(CancellationToken cancellationToken);
}

public sealed record LotActuals
{
    public required DateTimeOffset ActualStart { get; init; }

    // Null while the lot runs.
    public required DateTimeOffset? ActualEnd { get; init; }
}

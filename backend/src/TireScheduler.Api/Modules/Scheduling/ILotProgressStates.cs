namespace TireScheduler.Api.Modules.Scheduling;

// Port (DEC-16), implemented by Progress: which lots have started, for R8 (a started lot stays
// where it is, A17) and for removing the placements of a cancelled order (AC-50).
public interface ILotProgressStates
{
    // The lots among lotIds that have started; a lot missing from the answer has not.
    Task<IReadOnlyDictionary<int, StartedLotState>> StartedLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);
}

public enum StartedLotState
{
    InProgress,
    Completed,
}

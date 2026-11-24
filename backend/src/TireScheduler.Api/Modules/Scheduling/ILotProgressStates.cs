namespace TireScheduler.Api.Modules.Scheduling;

// Port (DEC-16), implemented by Progress: which lots have started, for R8 (a started lot stays
// where it is, A17) and for removing the placements of a cancelled order (AC-50), and when they ended.
public interface ILotProgressStates
{
    // The lots among lotIds that have started; a lot missing from the answer has not.
    Task<IReadOnlyDictionary<int, StartedLotState>> StartedLotsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);

    // The actual ends of the completed lots among lotIds, for the completion time of an order in
    // the proposal's late flag (DEC-62); a lot missing from the answer has not completed.
    Task<IReadOnlyDictionary<int, DateTimeOffset>> ActualEndsAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken);
}

public enum StartedLotState
{
    InProgress,
    Completed,
}

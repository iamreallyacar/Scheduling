namespace TireScheduler.Api.Modules.MasterData;

// Write port (DEC-44), implemented by Scheduling: after a shift or holiday change, the stored
// planned ends follow the new working time (A2, AC-182). Called inside the change's open
// transaction, after every resource row is locked, so the change and the ends commit together.
public interface IPlannedEndRecompute
{
    Task RecomputeAllAsync(CancellationToken cancellationToken);
}

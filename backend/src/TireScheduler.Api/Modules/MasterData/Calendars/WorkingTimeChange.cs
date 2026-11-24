namespace TireScheduler.Api.Modules.MasterData.Calendars;

// What every change to the working time does around its own write (DEC-44): lock every resource
// before writing, so no placement command checks against the old working time meanwhile, and
// recompute the planned ends after it.
internal sealed class WorkingTimeChange(IMasterDataLocks masterDataLocks, IPlannedEndRecompute plannedEnds)
{
    public Task LockEveryResourceAsync(CancellationToken cancellationToken) => masterDataLocks.LockEveryResourceAsync(cancellationToken);

    public Task RecomputePlannedEndsAsync(CancellationToken cancellationToken) => plannedEnds.RecomputeAllAsync(cancellationToken);
}

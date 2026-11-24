using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// Answers the MasterData write port (DEC-44): the planned ends of every version follow the
// working time that the open transaction has just changed (AC-182).
internal sealed class PlannedEndRecompute(
    SchedulingDbContext context,
    ScheduleSnapshotLoader snapshots,
    RequestTransaction requestTransaction) : IPlannedEndRecompute
{
    public async Task RecomputeAllAsync(CancellationToken cancellationToken)
    {
        requestTransaction.Join(context);
        List<int> versionIds = await context.ScheduleVersions.OrderBy(version => version.Id).Select(version => version.Id).ToListAsync(cancellationToken);
        foreach (int versionId in versionIds)
        {
            foreach (RecomputedEnd changedEnd in PlacementRules.RecomputeEnds(await snapshots.ForVersionAsync(versionId, cancellationToken)))
            {
                // A remove or a cancel deletes placements under the order lock only, so one read in
                // the snapshot can be gone by its update; a removed placement has no end to keep.
                // A tracked update would fail on the missing row, so this one updates whatever is left.
                await context.Placements
                    .Where(placement => placement.Id == changedEnd.PlacementId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(placement => placement.PlannedEnd, changedEnd.PlannedEnd), cancellationToken);
            }
        }
    }
}

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
            IReadOnlyList<RecomputedEnd> changedEnds = PlacementRules.RecomputeEnds(await snapshots.ForVersionAsync(versionId, cancellationToken));
            int[] changedPlacementIds = changedEnds.Select(changedEnd => changedEnd.PlacementId).ToArray();
            Dictionary<int, Placement> changedPlacements = await context.Placements
                .Where(placement => changedPlacementIds.Contains(placement.Id))
                .ToDictionaryAsync(placement => placement.Id, cancellationToken);
            foreach (RecomputedEnd changedEnd in changedEnds)
            {
                // A cancel removes placements under the order lock only, so one read in the
                // snapshot can be gone by now; a removed placement has no end to keep.
                if (changedPlacements.TryGetValue(changedEnd.PlacementId, out Placement? placement))
                {
                    placement.PlannedEnd = changedEnd.PlannedEnd;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

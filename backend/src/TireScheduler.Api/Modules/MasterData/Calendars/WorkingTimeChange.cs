using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData.Machines;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// What every change to the working time does around its own write (DEC-44): lock every resource
// before writing, so no placement command checks against the old working time meanwhile, and
// recompute the planned ends after it.
internal sealed class WorkingTimeChange(MasterDataDbContext context, IPlannedEndRecompute plannedEnds)
{
    // Ascending ids, so two changes never wait on each other.
    public async Task LockEveryResourceAsync(CancellationToken cancellationToken)
    {
        List<int> resourceIds = await context.Resources.OrderBy(resource => resource.Id).Select(resource => resource.Id).ToListAsync(cancellationToken);
        foreach (int resourceId in resourceIds)
        {
            await context.LockRowAsync<Resource>(resourceId, cancellationToken);
        }
    }

    public Task RecomputePlannedEndsAsync(CancellationToken cancellationToken) => plannedEnds.RecomputeAllAsync(cancellationToken);
}

using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Modules.Scheduling;

// Answers the Orders port (DEC-16) and its one write (DEC-33).
internal sealed class LotPlacements(
    SchedulingDbContext context,
    ScheduleVersions versions,
    ILotProgressStates lotProgress,
    RequestTransaction requestTransaction) : ILotPlacements
{
    public async Task<bool> AnyPlacedInLiveVersionAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        int liveVersionId = await versions.LiveVersionIdAsync(cancellationToken);
        return await context.Placements.AnyAsync(placement => placement.VersionId == liveVersionId && lotIds.Contains(placement.LotId), cancellationToken);
    }

    // A started lot keeps its placement as the record of where it runs or ran (A17).
    public async Task RemoveUnstartedAsync(IReadOnlyCollection<int> lotIds, CancellationToken cancellationToken)
    {
        requestTransaction.Join(context);
        IReadOnlyDictionary<int, StartedLotState> startedLots = await lotProgress.StartedLotsAsync(lotIds, cancellationToken);
        int[] unstartedLotIds = lotIds.Where(lotId => !startedLots.ContainsKey(lotId)).ToArray();
        await context.Placements.Where(placement => unstartedLotIds.Contains(placement.LotId)).ExecuteDeleteAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class PlacementReader(SchedulingDbContext context, ScheduleVersions versions) : IPlacementReader
{
    public async Task<IReadOnlySet<int>> PlacedLotIdsAsync(string versionId, CancellationToken cancellationToken)
    {
        int resolvedVersionId = await versions.ResolveAsync(versionId, cancellationToken);
        List<int> lotIds = await context.Placements
            .Where(placement => placement.VersionId == resolvedVersionId)
            .Select(placement => placement.LotId)
            .ToListAsync(cancellationToken);
        return lotIds.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<int, PlacementValues>> PlacementsOfLotsAsync(
        string versionId,
        IReadOnlyCollection<int> lotIds,
        CancellationToken cancellationToken)
    {
        int resolvedVersionId = await versions.ResolveAsync(versionId, cancellationToken);
        return await context.Placements
            .Where(placement => placement.VersionId == resolvedVersionId && lotIds.Contains(placement.LotId))
            .Select(placement => new PlacementValues
            {
                LotId = placement.LotId,
                ResourceId = placement.ResourceId,
                PlannedStart = placement.PlannedStart,
                PlannedEnd = placement.PlannedEnd,
            })
            .ToDictionaryAsync(placement => placement.LotId, cancellationToken);
    }
}

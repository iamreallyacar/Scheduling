using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class PlacementReader(SchedulingDbContext context, ScheduleVersions versions) : IPlacementReader
{
    private static readonly Expression<Func<Placement, PlacementValues>> ToPlacementValues = placement => new PlacementValues
    {
        Id = placement.Id,
        LotId = placement.LotId,
        ResourceId = placement.ResourceId,
        PlannedStart = placement.PlannedStart,
        PlannedEnd = placement.PlannedEnd,
    };

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
            .Select(ToPlacementValues)
            .ToDictionaryAsync(placement => placement.LotId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlacementValues>> PlacementsOverlappingAsync(
        string versionId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken)
    {
        int resolvedVersionId = await versions.ResolveAsync(versionId, cancellationToken);
        return await context.Placements
            .Where(placement => placement.VersionId == resolvedVersionId && placement.PlannedStart < rangeEnd && rangeStart < placement.PlannedEnd)
            .OrderBy(placement => placement.PlannedStart)
            .ThenBy(placement => placement.Id)
            .Select(ToPlacementValues)
            .ToListAsync(cancellationToken);
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Scheduling;

// Tells MasterData which placement is on a resource, in any version, so the resource is neither
// deleted (AC-20) nor given another type (AC-77) while lots are planned on it (A14).
internal sealed class PlacementReferences(SchedulingDbContext context) : IResourceReferences, IResourcePlacementReferences
{
    public Task<string?> FindItemUsingResourceAsync(int resourceId, CancellationToken cancellationToken) =>
        FindPlacementOnResourceAsync(resourceId, cancellationToken);

    public async Task<string?> FindPlacementOnResourceAsync(int resourceId, CancellationToken cancellationToken)
    {
        Placement? first = await context.Placements
            .Where(placement => placement.ResourceId == resourceId)
            .OrderBy(placement => placement.PlannedStart)
            .ThenBy(placement => placement.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return first is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"the placement of lot {first.LotId} from {first.PlannedStart.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
    }
}

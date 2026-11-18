using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Scheduling;

// Tells MasterData which unavailability is recorded on a resource (A14, AC-20).
internal sealed class UnavailabilityReferences(SchedulingDbContext context) : IResourceReferences
{
    public async Task<string?> FindItemUsingResourceAsync(int resourceId, CancellationToken cancellationToken)
    {
        Unavailability? first = await context.Unavailabilities
            .Where(unavailability => unavailability.ResourceId == resourceId)
            .OrderBy(unavailability => unavailability.StartsAt)
            .ThenBy(unavailability => unavailability.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (first is null)
        {
            return null;
        }

        string kind = first.Kind == UnavailabilityKind.Planned ? "planned maintenance" : "breakdown";
        return string.Create(CultureInfo.InvariantCulture, $"the {kind} from {first.StartsAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
    }
}

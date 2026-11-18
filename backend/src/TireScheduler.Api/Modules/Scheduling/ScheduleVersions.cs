using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.Scheduling;

// The schedule version a path names: `live` or a version id (DEC-17, A41).
internal sealed class ScheduleVersions(SchedulingDbContext context)
{
    // Another version that does not exist is answered 404 (AC-23).
    public async Task<int> ResolveAsync(string versionPath, CancellationToken cancellationToken)
    {
        if (versionPath == ScheduleVersionAlias.Live)
        {
            // The migration creates the live version, so a missing one is a bug, not a 404.
            return await LiveVersionIdAsync(cancellationToken);
        }

        int? versionId = int.TryParse(versionPath, NumberStyles.None, CultureInfo.InvariantCulture, out int requestedId)
            ? await context.ScheduleVersions.Where(version => version.Id == requestedId).Select(version => (int?)version.Id).SingleOrDefaultAsync(cancellationToken)
            : null;
        return versionId ?? throw ApiProblemException.NotFound("This schedule version");
    }

    public Task<int> LiveVersionIdAsync(CancellationToken cancellationToken) =>
        context.ScheduleVersions.Where(version => version.IsLive).Select(version => version.Id).SingleAsync(cancellationToken);
}

using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class UnavailabilityReader(SchedulingDbContext context) : IUnavailabilityReader
{
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<UnavailabilityKind>>> KindsCoveringAsync(
        IReadOnlyCollection<int> resourceIds,
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var covering = await context.Unavailabilities
            .Where(unavailability => resourceIds.Contains(unavailability.ResourceId)
                && unavailability.StartsAt <= instant
                && (unavailability.EndsAt == null || unavailability.EndsAt > instant))
            .Select(unavailability => new { unavailability.ResourceId, unavailability.Kind })
            .ToListAsync(cancellationToken);
        return covering
            .GroupBy(unavailability => unavailability.ResourceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<UnavailabilityKind>)[.. group.Select(unavailability => unavailability.Kind)]);
    }

    public async Task<IReadOnlyList<UnavailabilityResponse>> OverlappingAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken) =>
        await context.Unavailabilities
            .Where(unavailability => unavailability.StartsAt < rangeEnd && (unavailability.EndsAt == null || rangeStart < unavailability.EndsAt))
            .OrderBy(unavailability => unavailability.StartsAt)
            .ThenBy(unavailability => unavailability.Id)
            .Select(unavailability => new UnavailabilityResponse
            {
                Id = unavailability.Id,
                ResourceId = unavailability.ResourceId,
                Kind = unavailability.Kind,
                Start = unavailability.StartsAt,
                End = unavailability.EndsAt,
                Reason = unavailability.Reason,
            })
            .ToListAsync(cancellationToken);
}

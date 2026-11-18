namespace TireScheduler.Api.Modules.Scheduling;

// The public read interface of Scheduling for Queries, which derives the machine status from it
// (DEC-39) and shows the unavailabilities on the timeline (DEC-45).
public interface IUnavailabilityReader
{
    // The kinds of the unavailabilities of each resource that cover the instant: from the start
    // up to, not including, the end; without an end, from the start on. A resource with none is
    // missing from the answer.
    Task<IReadOnlyDictionary<int, IReadOnlyList<UnavailabilityKind>>> KindsCoveringAsync(
        IReadOnlyCollection<int> resourceIds,
        DateTimeOffset instant,
        CancellationToken cancellationToken);

    // The unavailabilities that overlap the half-open range, one without an end from its start
    // on, by start, then id.
    Task<IReadOnlyList<UnavailabilityResponse>> OverlappingAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken);
}

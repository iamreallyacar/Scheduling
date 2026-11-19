using System.Linq.Expressions;

namespace TireScheduler.Api.Modules.Scheduling;

// The one mapping from an unavailability row to its answer: an expression, so reads project it in
// the database, and its compiled form for a row just written.
internal static class UnavailabilityResponses
{
    public static readonly Expression<Func<Unavailability, UnavailabilityResponse>> FromRow = unavailability => new UnavailabilityResponse
    {
        Id = unavailability.Id,
        ResourceId = unavailability.ResourceId,
        Kind = unavailability.Kind,
        Start = unavailability.StartsAt,
        End = unavailability.EndsAt,
        Reason = unavailability.Reason,
    };

    private static readonly Func<Unavailability, UnavailabilityResponse> CompiledFromRow = FromRow.Compile();

    public static UnavailabilityResponse ToResponse(Unavailability unavailability) => CompiledFromRow(unavailability);
}

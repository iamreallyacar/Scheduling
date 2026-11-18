using TireScheduler.Api.Security;

namespace TireScheduler.Api.Queries.Schedule;

// The schedule reads of a version (DEC-17); the placement writes are mapped by the Scheduling
// module (PlacementEndpoints).
internal static class ScheduleViewEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder version = api.MapGroup("/versions/{versionId}").WithTags("Schedule");
        version.MapGet("/unscheduled-lots", async (string versionId, UnscheduledLotReader reader, CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.ListAsync(versionId, cancellationToken)))
            .WithName("ListUnscheduledLots")
            .RequireAuthorization(AccessPolicies.Read);
    }
}

using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Scheduling;

// The placement writes of a version (DEC-17) and the earliest-slot search that suggests one; the
// schedule reads are served by Queries.
internal static class PlacementEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder placement = api.MapGroup("/versions/{versionId}/lots/{lotId:int}/placement").WithTags("Schedule");
        placement.MapPut("/", async (string versionId, int lotId, LotPlacementRequest request, PlacementRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.PlaceAsync(versionId, lotId, request, cancellationToken)))
            .WithName("PlaceLot")
            .RequireAuthorization(AccessPolicies.Plan);
        placement.MapDelete("/", async (string versionId, int lotId, PlacementRegister register, CancellationToken cancellationToken) =>
            {
                await register.RemoveAsync(versionId, lotId, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("RemoveLotPlacement")
            .RequireAuthorization(AccessPolicies.Plan);
        api.MapGet("/versions/{versionId}/lots/{lotId:int}/earliest-slot", async (string versionId, int lotId, int? resourceId, DateTimeOffset? notBefore, EarliestSlotSearch search, CancellationToken cancellationToken) =>
                TypedResults.Ok(await search.FindAsync(versionId, lotId, resourceId, notBefore, cancellationToken)))
            .WithTags("Schedule")
            .WithName("GetEarliestSlot")
            .RequireAuthorization(AccessPolicies.Read);
    }
}

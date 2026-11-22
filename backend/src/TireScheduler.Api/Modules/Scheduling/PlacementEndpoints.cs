using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Scheduling;

// The placement writes of a version (DEC-17), the earliest-slot search that suggests one, and the
// whole-order proposal with its commit (DEC-60); the schedule reads are served by Queries.
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
        RouteGroupBuilder proposal = api.MapGroup("/versions/{versionId}/orders/{orderId:int}/asap-proposal").WithTags("Schedule");
        proposal.MapPost("/", async (string versionId, int orderId, DateTimeOffset? notBefore, OrderProposalSearch search, CancellationToken cancellationToken) =>
                TypedResults.Ok(await search.ProposeAsync(versionId, orderId, notBefore, cancellationToken)))
            .WithName("ProposeOrderAsap")
            .RequireAuthorization(AccessPolicies.Plan);
        proposal.MapPost("/commit", async (string versionId, int orderId, OrderProposalCommitRequest request, PlacementRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.CommitProposalAsync(versionId, orderId, request, cancellationToken)))
            .WithName("CommitOrderAsapProposal")
            .RequireAuthorization(AccessPolicies.Plan);
    }
}

using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Progress;

// The progress writes of a lot (DEC-62); the reads that show progress are served by Queries.
internal static class ProgressEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder progress = api.MapGroup("/lots/{lotId:int}/progress").WithTags("Progress");
        progress.MapPut("/", async (int lotId, LotProgressRequest request, ProgressRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.RecordAsync(lotId, request, cancellationToken)))
            .WithName("RecordLotProgress")
            .RequireAuthorization(AccessPolicies.Plan);
        progress.MapDelete("/", async (int lotId, ProgressRegister register, CancellationToken cancellationToken) =>
            {
                await register.RemoveAsync(lotId, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("RemoveLotProgress")
            .RequireAuthorization(AccessPolicies.Plan);
    }
}

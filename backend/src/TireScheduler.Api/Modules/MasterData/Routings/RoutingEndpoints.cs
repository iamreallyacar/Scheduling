using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.MasterData.Routings;

internal static class RoutingEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder routing = api.MapGroup("/products/{productId:int}/routing").WithTags("Products");
        routing.MapGet("/", async (int productId, RoutingRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.GetAsync(productId, cancellationToken).ConfigureAwait(false)))
            .WithName("GetRouting")
            .RequireAuthorization(AccessPolicies.Read);
        routing.MapPut("/", async (int productId, RoutingRequest request, RoutingRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ReplaceAsync(productId, request, cancellationToken).ConfigureAwait(false)))
            .WithRequestValidation<RoutingRequest>()
            .WithName("ReplaceRouting")
            .RequireAuthorization(AccessPolicies.Administer);
    }
}

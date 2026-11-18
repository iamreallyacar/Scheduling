using TireScheduler.Api.Security;

namespace TireScheduler.Api.Queries.Machines;

// The machine reads; the writes are mapped by the MasterData module (DEC-39).
internal static class MachineViewEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder resources = api.MapGroup("/resources").WithTags("Machines");
        resources.MapGet("/", async (int? resourceTypeId, bool? isActive, MachineViewReader reader, CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.ListAsync(resourceTypeId, isActive, cancellationToken)))
            .WithName("ListResources")
            .RequireAuthorization(AccessPolicies.Read);
        resources.MapGet("/{id:int}", async (int id, MachineViewReader reader, CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.GetAsync(id, cancellationToken)))
            .WithName("GetResource")
            .RequireAuthorization(AccessPolicies.Read);
    }
}

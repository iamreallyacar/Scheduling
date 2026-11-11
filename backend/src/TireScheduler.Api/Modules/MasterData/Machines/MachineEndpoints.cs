using Microsoft.AspNetCore.Http.HttpResults;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.MasterData.Machines;

internal static class MachineEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder resourceTypes = api.MapGroup("/resource-types").WithTags("Machines");
        resourceTypes.MapGet("/", async (ResourceTypeRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ListAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("ListResourceTypes")
            .RequireAuthorization(AccessPolicies.Read);
        resourceTypes.MapPost("/", async Task<Created<ResourceTypeResponse>> (ResourceTypeRequest request, ResourceTypeRegister register, CancellationToken cancellationToken) =>
            {
                ResourceTypeResponse created = await register.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                return TypedResults.Created($"/api/v1/resource-types/{created.Id}", created);
            })
            .WithRequestValidation<ResourceTypeRequest>()
            .WithName("CreateResourceType")
            .RequireAuthorization(AccessPolicies.Administer);
        resourceTypes.MapPut("/{id:int}", async (int id, ResourceTypeRequest request, ResourceTypeRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false)))
            .WithRequestValidation<ResourceTypeRequest>()
            .WithName("UpdateResourceType")
            .RequireAuthorization(AccessPolicies.Administer);
        resourceTypes.MapDelete("/{id:int}", async (int id, ResourceTypeRegister register, CancellationToken cancellationToken) =>
            {
                await register.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
                return TypedResults.NoContent();
            })
            .WithName("DeleteResourceType")
            .RequireAuthorization(AccessPolicies.Administer);

        RouteGroupBuilder resources = api.MapGroup("/resources").WithTags("Machines");
        resources.MapGet("/", async (int? resourceTypeId, bool? isActive, MachineRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ListAsync(resourceTypeId, isActive, cancellationToken).ConfigureAwait(false)))
            .WithName("ListResources")
            .RequireAuthorization(AccessPolicies.Read);
        resources.MapGet("/{id:int}", async (int id, MachineRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.GetAsync(id, cancellationToken).ConfigureAwait(false)))
            .WithName("GetResource")
            .RequireAuthorization(AccessPolicies.Read);
        resources.MapPost("/", async Task<Created<ResourceResponse>> (CreateResourceRequest request, MachineRegister register, CancellationToken cancellationToken) =>
            {
                ResourceResponse created = await register.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                return TypedResults.Created($"/api/v1/resources/{created.Id}", created);
            })
            .WithRequestValidation<CreateResourceRequest>()
            .WithName("CreateResource")
            .RequireAuthorization(AccessPolicies.Administer);
        resources.MapPut("/{id:int}", async (int id, UpdateResourceRequest request, MachineRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false)))
            .WithRequestValidation<UpdateResourceRequest>()
            .WithName("UpdateResource")
            .RequireAuthorization(AccessPolicies.Administer);
        resources.MapDelete("/{id:int}", async (int id, MachineRegister register, CancellationToken cancellationToken) =>
            {
                await register.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
                return TypedResults.NoContent();
            })
            .WithName("DeleteResource")
            .RequireAuthorization(AccessPolicies.Administer);
    }
}

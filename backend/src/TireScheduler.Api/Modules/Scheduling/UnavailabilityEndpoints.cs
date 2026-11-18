using Microsoft.AspNetCore.Http.HttpResults;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Scheduling;

internal static class UnavailabilityEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder unavailabilities = api.MapGroup("/resources/{resourceId:int}/unavailabilities").WithTags("Unavailability");
        unavailabilities.MapGet("/", async (int resourceId, UnavailabilityRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ListAsync(resourceId, cancellationToken)))
            .WithName("ListUnavailabilities")
            .RequireAuthorization(AccessPolicies.Read);
        unavailabilities.MapPost("/", async Task<Created<UnavailabilityResponse>> (int resourceId, UnavailabilityRequest request, UnavailabilityRegister register, CancellationToken cancellationToken) =>
            {
                UnavailabilityResponse created = await register.CreateAsync(resourceId, request, cancellationToken);
                return TypedResults.Created($"/api/v1/resources/{resourceId}/unavailabilities/{created.Id}", created);
            })
            .WithName("CreateUnavailability")
            .RequireAuthorization(AccessPolicies.Plan);
        unavailabilities.MapPut("/{id:int}", async (int resourceId, int id, UnavailabilityRequest request, UnavailabilityRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.UpdateAsync(resourceId, id, request, cancellationToken)))
            .WithName("UpdateUnavailability")
            .RequireAuthorization(AccessPolicies.Plan);
        unavailabilities.MapDelete("/{id:int}", async (int resourceId, int id, UnavailabilityRegister register, CancellationToken cancellationToken) =>
            {
                await register.DeleteAsync(resourceId, id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeleteUnavailability")
            .RequireAuthorization(AccessPolicies.Plan);
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.MasterData.Products;

internal static class ProductEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder products = api.MapGroup("/products").WithTags("Products");
        products.MapGet("/", async (ProductRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ListAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("ListProducts")
            .RequireAuthorization(AccessPolicies.Read);
        products.MapGet("/{id:int}", async (int id, ProductRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.GetAsync(id, cancellationToken).ConfigureAwait(false)))
            .WithName("GetProduct")
            .RequireAuthorization(AccessPolicies.Read);
        products.MapPost("/", async Task<Created<ProductResponse>> (ProductRequest request, ProductRegister register, CancellationToken cancellationToken) =>
            {
                ProductResponse created = await register.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                return TypedResults.Created($"/api/v1/products/{created.Id}", created);
            })
            .WithRequestValidation<ProductRequest>()
            .WithName("CreateProduct")
            .RequireAuthorization(AccessPolicies.Administer);
        products.MapPut("/{id:int}", async (int id, ProductRequest request, ProductRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false)))
            .WithRequestValidation<ProductRequest>()
            .WithName("UpdateProduct")
            .RequireAuthorization(AccessPolicies.Administer);
        products.MapDelete("/{id:int}", async (int id, ProductRegister register, CancellationToken cancellationToken) =>
            {
                await register.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
                return TypedResults.NoContent();
            })
            .WithName("DeleteProduct")
            .RequireAuthorization(AccessPolicies.Administer);
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Orders;

internal static class OrderEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        // The reads are served by Queries, which adds the derived status (DEC-29).
        RouteGroupBuilder orders = api.MapGroup("/orders").WithTags("Orders");
        orders.MapPost("/", async Task<Created<OrderCreatedResponse>> (OrderRequest request, OrderRegister register, CancellationToken cancellationToken) =>
            {
                int orderId = await register.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/v1/orders/{orderId}", new OrderCreatedResponse { Id = orderId });
            })
            .WithName("CreateOrder")
            .RequireAuthorization(AccessPolicies.Plan);
        orders.MapPut("/{id:int}", async (int id, OrderRequest request, OrderRegister register, CancellationToken cancellationToken) =>
            {
                await register.UpdateAsync(id, request, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("UpdateOrder")
            .RequireAuthorization(AccessPolicies.Plan);
        // Cancelling is a state change, not a delete (ARCHITECTURE.md section "Persistence rules").
        orders.MapPost("/{id:int}/cancel", async (int id, OrderRegister register, CancellationToken cancellationToken) =>
            {
                await register.CancelAsync(id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("CancelOrder")
            .RequireAuthorization(AccessPolicies.Plan);
        orders.MapDelete("/{id:int}", async (int id, OrderRegister register, CancellationToken cancellationToken) =>
            {
                await register.DeleteAsync(id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeleteOrder")
            .RequireAuthorization(AccessPolicies.Plan);
    }
}

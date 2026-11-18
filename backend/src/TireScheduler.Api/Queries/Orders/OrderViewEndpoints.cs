using TireScheduler.Api.Security;

namespace TireScheduler.Api.Queries.Orders;

// The order reads; the writes are mapped by the Orders module (DEC-29).
internal static class OrderViewEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder orders = api.MapGroup("/orders").WithTags("Orders");
        orders.MapGet(
                "/",
                async (
                    int? page,
                    int? pageSize,
                    string? search,
                    string[]? status,
                    bool? late,
                    string? customer,
                    DateOnly? dueFrom,
                    DateOnly? dueTo,
                    string[]? priority,
                    string? sort,
                    OrderViewReader reader,
                    CancellationToken cancellationToken) =>
                {
                    OrderListRequest request = new OrderListParameters
                    {
                        Page = page,
                        PageSize = pageSize,
                        Search = search,
                        Statuses = status ?? [],
                        Late = late,
                        Customer = customer,
                        DueFrom = dueFrom,
                        DueTo = dueTo,
                        Priorities = priority ?? [],
                        Sort = sort,
                    }.ToRequest();
                    return TypedResults.Ok(await reader.ListAsync(request, cancellationToken));
                })
            .WithName("ListOrders")
            .RequireAuthorization(AccessPolicies.Read);
        orders.MapGet("/{id:int}", async (int id, OrderViewReader reader, CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.GetAsync(id, cancellationToken)))
            .WithName("GetOrder")
            .RequireAuthorization(AccessPolicies.Read);
    }
}

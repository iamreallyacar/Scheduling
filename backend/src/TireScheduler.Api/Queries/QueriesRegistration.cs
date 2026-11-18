using TireScheduler.Api.Queries.Orders;

namespace TireScheduler.Api.Queries;

// Registration point for the read-only views that span modules (ARCHITECTURE.md section "Structure").
public static class QueriesRegistration
{
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        services.AddScoped<OrderViewReader>();
        return services;
    }

    public static RouteGroupBuilder MapQueriesEndpoints(this RouteGroupBuilder api)
    {
        OrderViewEndpoints.Map(api);
        return api;
    }
}

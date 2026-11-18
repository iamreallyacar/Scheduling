using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Orders;

// Registration point for the Orders module (ARCHITECTURE.md section "Structure").
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<OrdersDbContext>(OrdersDbContext.Schema);
        services.AddScoped<OrderRegister>();
        services.AddScoped<IOrderReader, OrderReader>();
        services.AddScoped<IProductReferences, OrderReferences>();
        services.AddScoped<IResourceTypeReferences, OrderReferences>();
        return services;
    }

    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder api)
    {
        OrderEndpoints.Map(api);
        return api;
    }
}

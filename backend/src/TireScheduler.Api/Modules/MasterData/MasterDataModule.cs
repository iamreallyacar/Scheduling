using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.MasterData.Products;
using TireScheduler.Api.Modules.MasterData.Routings;

namespace TireScheduler.Api.Modules.MasterData;

// Registration point for the MasterData module (ARCHITECTURE.md section 2).
public static class MasterDataModule
{
    public static IServiceCollection AddMasterDataModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<MasterDataDbContext>(MasterDataDbContext.Schema);
        services.AddScoped<ResourceTypeRegister>();
        services.AddScoped<MachineRegister>();
        services.AddScoped<ProductRegister>();
        services.AddScoped<RoutingRegister>();
        return services;
    }

    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder api = endpoints.MapGroup("/api/v1");
        MachineEndpoints.Map(api);
        ProductEndpoints.Map(api);
        RoutingEndpoints.Map(api);
        return endpoints;
    }
}

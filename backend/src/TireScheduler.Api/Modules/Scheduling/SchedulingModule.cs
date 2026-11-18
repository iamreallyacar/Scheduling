using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Modules.Scheduling;

// Registration point for the Scheduling module (ARCHITECTURE.md section "Structure").
public static class SchedulingModule
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<SchedulingDbContext>(SchedulingDbContext.Schema);
        services.AddScoped<UnavailabilityRegister>();
        services.AddScoped<IUnavailabilityReader, UnavailabilityReader>();
        services.AddScoped<IResourceReferences, UnavailabilityReferences>();
        services.AddScoped<ILotPlacements, LotPlacements>();
        return services;
    }

    public static RouteGroupBuilder MapSchedulingEndpoints(this RouteGroupBuilder api)
    {
        UnavailabilityEndpoints.Map(api);
        return api;
    }
}

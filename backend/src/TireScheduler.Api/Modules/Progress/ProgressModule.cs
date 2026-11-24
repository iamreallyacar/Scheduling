using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Modules.Progress;

// Registration point for the Progress module (ARCHITECTURE.md section "Structure").
public static class ProgressModule
{
    public static IServiceCollection AddProgressModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<ProgressDbContext>(ProgressDbContext.Schema);
        services.AddScoped<ProgressRegister>();
        services.AddScoped<LotProgressFacts>();
        services.AddScoped<ILotProgress>(provider => provider.GetRequiredService<LotProgressFacts>());
        services.AddScoped<ILotProgressStates>(provider => provider.GetRequiredService<LotProgressFacts>());
        services.AddScoped<ILotProgressReader>(provider => provider.GetRequiredService<LotProgressFacts>());
        return services;
    }

    public static RouteGroupBuilder MapProgressEndpoints(this RouteGroupBuilder api)
    {
        ProgressEndpoints.Map(api);
        return api;
    }
}

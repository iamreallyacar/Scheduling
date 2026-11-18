using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Modules.Progress;

// Registration point for the Progress module (ARCHITECTURE.md section "Structure"). Its services,
// database schema and endpoints are added here by the milestone tasks.
public static class ProgressModule
{
    public static IServiceCollection AddProgressModule(this IServiceCollection services)
    {
        services.AddScoped<LotProgressFacts>();
        services.AddScoped<ILotProgress>(provider => provider.GetRequiredService<LotProgressFacts>());
        services.AddScoped<ILotProgressStates>(provider => provider.GetRequiredService<LotProgressFacts>());
        return services;
    }
}

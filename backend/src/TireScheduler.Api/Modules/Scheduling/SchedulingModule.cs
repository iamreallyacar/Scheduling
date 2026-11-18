using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Modules.Scheduling;

// Registration point for the Scheduling module (ARCHITECTURE.md section "Structure"). Its services,
// database schema and endpoints are added here by the milestone tasks.
public static class SchedulingModule
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services) =>
        services.AddScoped<ILotPlacements, LotPlacements>();
}

using TireScheduler.Api.Queries.Machines;
using TireScheduler.Api.Queries.Orders;
using TireScheduler.Api.Queries.Schedule;

namespace TireScheduler.Api.Queries;

// Registration point for the read-only views that span modules (ARCHITECTURE.md section "Structure").
public static class QueriesRegistration
{
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        services.AddScoped<OrderViewReader>();
        services.AddScoped<MachineViewReader>();
        services.AddScoped<UnscheduledLotReader>();
        services.AddScoped<ConflictedPlacementReader>();
        services.AddScoped<TimelineReader>();
        return services;
    }

    public static RouteGroupBuilder MapQueriesEndpoints(this RouteGroupBuilder api)
    {
        OrderViewEndpoints.Map(api);
        MachineViewEndpoints.Map(api);
        ScheduleViewEndpoints.Map(api);
        return api;
    }
}

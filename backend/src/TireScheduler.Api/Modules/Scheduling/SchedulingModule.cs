using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Modules.Scheduling;

// Registration point for the Scheduling module (ARCHITECTURE.md section "Structure").
public static class SchedulingModule
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services)
    {
        services.AddOptions<SchedulingOptions>()
            .BindConfiguration(SchedulingOptions.SectionName)
            .Validate(options => options.SearchHorizonDays > 0, $"Setting {SchedulingOptions.SectionName}:{nameof(SchedulingOptions.SearchHorizonDays)} must be more than 0.")
            .ValidateOnStart();
        services.AddModuleDbContext<SchedulingDbContext>(SchedulingDbContext.Schema);
        services.AddScoped<UnavailabilityRegister>();
        services.AddScoped<IUnavailabilityReader, UnavailabilityReader>();
        services.AddScoped<ScheduleVersions>();
        services.AddScoped<EngineWorkingTimeLoader>();
        services.AddScoped<IWorkingPeriodReader, WorkingPeriodReader>();
        services.AddScoped<ScheduleSnapshotLoader>();
        services.AddScoped<PlacementRegister>();
        services.AddScoped<EarliestSlotSearch>();
        services.AddScoped<OrderProposalSearch>();
        services.AddScoped<IPlacementReader, PlacementReader>();
        services.AddScoped<IResourceReferences, UnavailabilityReferences>();
        services.AddScoped<PlacementReferences>();
        services.AddScoped<IResourceReferences>(provider => provider.GetRequiredService<PlacementReferences>());
        services.AddScoped<IResourcePlacementReferences>(provider => provider.GetRequiredService<PlacementReferences>());
        services.AddScoped<ILotPlacements, LotPlacements>();
        services.AddScoped<IPlannedEndRecompute, PlannedEndRecompute>();
        services.AddScoped<IPlacementConflictReader, PlacementConflictReader>();
        return services;
    }

    public static RouteGroupBuilder MapSchedulingEndpoints(this RouteGroupBuilder api)
    {
        UnavailabilityEndpoints.Map(api);
        PlacementEndpoints.Map(api);
        return api;
    }
}

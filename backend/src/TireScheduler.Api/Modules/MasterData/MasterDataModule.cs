using Microsoft.Extensions.Options;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData.Calendars;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.MasterData.Products;
using TireScheduler.Api.Modules.MasterData.Routings;

namespace TireScheduler.Api.Modules.MasterData;

// Registration point for the MasterData module (ARCHITECTURE.md section "Structure").
public static class MasterDataModule
{
    public static IServiceCollection AddMasterDataModule(this IServiceCollection services)
    {
        services.AddOptions<PlantOptions>().BindConfiguration(PlantOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<PlantOptions>, PlantOptionsValidator>();
        services.AddModuleDbContext<MasterDataDbContext>(MasterDataDbContext.Schema);
        services.AddScoped<CalendarRegister>();
        services.AddScoped<HolidayRegister>();
        services.AddScoped<WorkingTimeChange>();
        services.AddScoped<ResourceTypeRegister>();
        services.AddScoped<MachineRegister>();
        services.AddScoped<ProductRegister>();
        services.AddScoped<RoutingRegister>();
        services.AddScoped<IMasterDataReader, MasterDataReader>();
        services.AddScoped<IMasterDataLocks, MasterDataLocks>();
        return services;
    }

    public static RouteGroupBuilder MapMasterDataEndpoints(this RouteGroupBuilder api)
    {
        MachineEndpoints.Map(api);
        CalendarEndpoints.Map(api);
        ProductEndpoints.Map(api);
        RoutingEndpoints.Map(api);
        return api;
    }
}

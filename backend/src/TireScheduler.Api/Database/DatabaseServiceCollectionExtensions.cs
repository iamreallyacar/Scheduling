using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace TireScheduler.Api.Database;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerDatabase(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"Missing setting ConnectionStrings:{DatabaseOptions.ConnectionStringName} "
                + $"(environment variable ConnectionStrings__{DatabaseOptions.ConnectionStringName}).")
            // Checked when the host starts, so a missing setting stops startup instead of the first request.
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
            NpgsqlDataSource.Create(serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));

        return services;
    }

    // Each module keeps its tables and its migration history in its own schema
    // (ARCHITECTURE.md section 8).
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) => options.UseNpgsql(
            serviceProvider.GetRequiredService<NpgsqlDataSource>(),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)));
        services.AddScoped<IStartupStep, MigrateDatabaseStep<TContext>>();
        return services;
    }
}

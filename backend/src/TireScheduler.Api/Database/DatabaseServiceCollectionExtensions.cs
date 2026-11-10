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
}

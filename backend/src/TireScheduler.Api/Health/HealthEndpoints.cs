using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace TireScheduler.Api.Health;

public static class HealthEndpoints
{
    public const string LivePath = "/api/v1/health/live";
    public const string ReadyPath = "/api/v1/health/ready";

    private const string ReadyTag = "ready";

    public static IServiceCollection AddSchedulerHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: [ReadyTag]);
        return services;
    }

    // Live: the process answers. Ready: the process can also reach its database. Both are open
    // without a session, because the hosting platform calls them.
    public static IEndpointRouteBuilder MapSchedulerHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) }).AllowAnonymous();
        return endpoints;
    }
}

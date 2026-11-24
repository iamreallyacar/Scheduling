using TireScheduler.Api.Security;

namespace TireScheduler.Api.Queries.Dashboard;

// The dashboard of a version (DEC-17, DEC-65).
internal static class DashboardEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGroup("/versions/{versionId}").WithTags("Dashboard")
            .MapGet("/dashboard", async (string versionId, DashboardReader reader, CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.GetAsync(versionId, cancellationToken)))
            .WithName("GetDashboard")
            .RequireAuthorization(AccessPolicies.Read);
    }
}

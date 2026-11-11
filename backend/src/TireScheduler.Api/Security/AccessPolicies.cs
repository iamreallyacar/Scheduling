using Microsoft.AspNetCore.Authorization;

namespace TireScheduler.Api.Security;

// The three access levels of ARCHITECTURE.md section 6. Every endpoint names one of them.
public static class AccessPolicies
{
    // Read everything: Viewer, Planner, Admin.
    public const string Read = "Read";

    // Edit orders, placements, unavailabilities and lot progress: Planner, Admin.
    public const string Plan = "Plan";

    // Edit master data and users: Admin.
    public const string Administer = "Administer";

    public static IServiceCollection AddAccessPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Read, policy => policy.RequireRole(RoleNames(UserRole.Viewer, UserRole.Planner, UserRole.Admin)))
            .AddPolicy(Plan, policy => policy.RequireRole(RoleNames(UserRole.Planner, UserRole.Admin)))
            .AddPolicy(Administer, policy => policy.RequireRole(RoleNames(UserRole.Admin)))
            // An endpoint that forgot its policy still needs a session; AC-14 keeps that from happening.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    private static string[] RoleNames(params UserRole[] roles) => roles.Select(role => role.ToString()).ToArray();
}

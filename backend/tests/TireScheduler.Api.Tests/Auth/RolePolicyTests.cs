using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Health;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.Auth;

// F-AUTH-2 over the full list of endpoints, so a new endpoint is covered without a new test.
// Which policy an endpoint must carry is checked by the tests of that endpoint's feature.
public sealed class RolePolicyTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    // ARCHITECTURE.md section 6.
    private static readonly Dictionary<string, UserRole[]> RolesAllowedByPolicy = new()
    {
        [AccessPolicies.Read] = [UserRole.Viewer, UserRole.Planner, UserRole.Admin],
        [AccessPolicies.Plan] = [UserRole.Planner, UserRole.Admin],
        [AccessPolicies.Administer] = [UserRole.Admin],
    };

    private static readonly string[] EndpointsOpenWithoutSession =
    [
        "GET /api/v1/auth/antiforgery-token",
        "POST /api/v1/auth/sign-in",
        $"GET {HealthEndpoints.LivePath}",
        $"GET {HealthEndpoints.ReadyPath}",
    ];

    [Fact]
    public void AC_14_every_endpoint_names_an_access_policy()
    {
        List<string> openEndpoints = [];
        foreach (RouteEndpoint endpoint in AllEndpoints())
        {
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                openEndpoints.Add(Describe(endpoint));
                continue;
            }

            string? policy = endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy;
            Assert.True(policy is not null && RolesAllowedByPolicy.ContainsKey(policy), $"{Describe(endpoint)} names no access policy.");
        }

        Assert.Equal(EndpointsOpenWithoutSession.Order(), openEndpoints.Order());
    }

    [Theory]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.Planner)]
    [InlineData(UserRole.Admin)]
    public async Task AC_9_to_AC_13_a_role_passes_exactly_the_endpoints_its_policies_allow(UserRole role)
    {
        using ApiSession session = await api.Factory.SignInAsRoleAsync(role);

        foreach (RouteEndpoint endpoint in AllEndpoints())
        {
            string? policy = endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy;
            if (policy is null)
            {
                continue;
            }

            HttpMethod method = new(HttpMethodOf(endpoint));
            // Sent without an antiforgery token: a state-changing request that passes the role
            // check is then stopped by the token check, so this test changes no data.
            HttpResponseMessage response = await session.SendAsync(method, PathOf(endpoint), withAntiforgeryToken: false);

            if (!RolesAllowedByPolicy[policy].Contains(role))
            {
                await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
            }
            else if (method == HttpMethod.Get)
            {
                Assert.False(
                    response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                    $"{role} was refused on {Describe(endpoint)} with {response.StatusCode}.");
            }
            else
            {
                await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.AuthAntiforgery);
            }
        }
    }

    [Theory]
    [InlineData(UserRole.Viewer, false, false)]
    [InlineData(UserRole.Planner, true, false)]
    [InlineData(UserRole.Admin, true, true)]
    public async Task AC_139_me_tells_the_ui_what_the_role_may_write(UserRole role, bool canPlan, bool canAdminister)
    {
        using ApiSession session = await api.Factory.SignInAsRoleAsync(role);

        JsonElement me = await session.GetJsonAsync("/api/v1/me");

        Assert.Equal(role.ToString(), me.GetProperty("role").GetString());
        Assert.Equal(canPlan, me.GetProperty("canPlan").GetBoolean());
        Assert.Equal(canAdminister, me.GetProperty("canAdminister").GetBoolean());
    }

    private IEnumerable<RouteEndpoint> AllEndpoints() =>
        api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    private static string Describe(RouteEndpoint endpoint) => $"{HttpMethodOf(endpoint)} {endpoint.RoutePattern.RawText}";

    private static string HttpMethodOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single() ?? "GET";

    // Any id works: the role check runs before the endpoint looks the item up.
    private static string PathOf(RouteEndpoint endpoint) =>
        string.Concat(endpoint.RoutePattern.PathSegments.Select(segment => "/" + (segment.IsSimple && segment.Parts[0].IsParameter ? "1" : string.Concat(segment.Parts.OfType<Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart>().Select(part => part.Content)))));
}

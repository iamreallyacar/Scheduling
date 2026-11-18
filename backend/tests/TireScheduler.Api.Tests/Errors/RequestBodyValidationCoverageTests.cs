using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Tests.Errors;

// Every endpoint of the real API that reads a body must check it (AC-163), so a module that
// maps endpoints outside the validated /api/v1 group fails here instead of answering a
// missing field with a 500.
public sealed class RequestBodyValidationCoverageTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    [Fact]
    public void AC_163_every_endpoint_that_reads_a_body_checks_it()
    {
        RouteEndpoint[] endpointsWithBody = api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType is not null)
            .ToArray();

        Assert.NotEmpty(endpointsWithBody);
        string[] uncheckedEndpoints = endpointsWithBody
            .Where(endpoint => endpoint.Metadata.GetMetadata<ValidatedRequestBodyMetadata>() is null)
            .Select(endpoint => endpoint.DisplayName ?? endpoint.RoutePattern.RawText ?? "unnamed endpoint")
            .ToArray();
        Assert.Empty(uncheckedEndpoints);
    }
}

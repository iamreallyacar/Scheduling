using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using TireScheduler.Api.Health;

namespace TireScheduler.Api.Tests;

public sealed class HealthEndpointTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    [Theory]
    [InlineData(HealthEndpoints.LivePath)]
    [InlineData(HealthEndpoints.ReadyPath)]
    public async Task AC_138_health_endpoint_answers_ok_without_a_session_over_plain_http(string path)
    {
        // The default test client uses http, as a health probe inside the hosting platform does.
        using HttpClient client = api.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_check_is_unhealthy_when_the_database_is_unreachable()
    {
        // Port 1 on localhost has no PostgreSQL, so the connection is refused at once.
        await using NpgsqlDataSource unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=none;Password=none;Timeout=2");
        PostgresHealthCheck check = new(unreachable);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Startup_fails_naming_the_setting_when_the_connection_string_is_missing()
    {
        await using SchedulerApiFactory factory = new(connectionString: null);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Scheduler", exception.Message, StringComparison.Ordinal);
    }
}

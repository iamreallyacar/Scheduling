using System.Net;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Health;

namespace TireScheduler.Api.Tests;

public sealed class HealthEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Live_answers_ok()
    {
        await using SchedulerApiFactory factory = new(postgres.ConnectionString);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(HealthEndpoints.LivePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_answers_ok_when_the_database_is_reachable()
    {
        await using SchedulerApiFactory factory = new(postgres.ConnectionString);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(HealthEndpoints.ReadyPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_answers_service_unavailable_when_the_database_is_unreachable()
    {
        // Port 1 on localhost has no PostgreSQL, so the connection is refused at once.
        await using SchedulerApiFactory factory = new("Host=127.0.0.1;Port=1;Username=none;Password=none;Timeout=2");
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(HealthEndpoints.ReadyPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Startup_fails_naming_the_setting_when_the_connection_string_is_missing()
    {
        await using SchedulerApiFactory factory = new(connectionString: null);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Scheduler", exception.Message, StringComparison.Ordinal);
    }
}

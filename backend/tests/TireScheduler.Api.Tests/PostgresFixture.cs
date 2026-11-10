using Testcontainers.PostgreSql;

namespace TireScheduler.Api.Tests;

// One real PostgreSQL container per test run (ARCHITECTURE.md section 9). Needs Docker.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync() => await container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await container.DisposeAsync().ConfigureAwait(false);
}

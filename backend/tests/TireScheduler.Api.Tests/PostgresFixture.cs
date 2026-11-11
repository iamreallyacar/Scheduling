using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(TireScheduler.Api.Tests.PostgresFixture))]

namespace TireScheduler.Api.Tests;

// One real PostgreSQL container per test run (ARCHITECTURE.md section 9). Needs Docker.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public async ValueTask InitializeAsync() => await container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await container.DisposeAsync().ConfigureAwait(false);

    // A database name that no other test uses, so tests cannot see each other's rows. The
    // database itself is created by the API's migrations at startup.
    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = $"test_{Guid.NewGuid():N}" }.ConnectionString;
}

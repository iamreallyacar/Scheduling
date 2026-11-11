namespace TireScheduler.Api.Tests;

// One running API on its own empty database, shared by the tests of one class.
public sealed class SchedulerApiFixture(PostgresFixture postgres) : IAsyncDisposable
{
    public string ConnectionString { get; } = postgres.NewDatabaseConnectionString();

    public SchedulerApiFactory Factory => field ??= new SchedulerApiFactory(ConnectionString);

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}

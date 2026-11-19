using Microsoft.Extensions.DependencyInjection;

namespace TireScheduler.Api.Tests;

// One running API on its own empty database. As a class fixture it is shared by the tests of one
// class; a class whose tests each need a database, clock or replaced services of their own makes
// one with ForEachTest in its constructor and disposes it.
public sealed class SchedulerApiFixture : IAsyncDisposable
{
    private readonly TimeProvider? clock;
    private readonly Action<IServiceCollection>? replaceServices;
    private SchedulerApiFactory? factory;

    // xUnit builds a class fixture through its only public constructor and cannot fill optional
    // arguments, so the clock and the replaced services come through ForEachTest.
    public SchedulerApiFixture(PostgresFixture postgres)
        : this(postgres, clock: null, replaceServices: null)
    {
    }

    private SchedulerApiFixture(PostgresFixture postgres, TimeProvider? clock, Action<IServiceCollection>? replaceServices)
    {
        ConnectionString = postgres.NewDatabaseConnectionString();
        this.clock = clock;
        this.replaceServices = replaceServices;
    }

    public string ConnectionString { get; }

    public SchedulerApiFactory Factory => factory ??= new SchedulerApiFactory(ConnectionString, clock: clock, replaceServices: replaceServices);

    public static SchedulerApiFixture ForEachTest(PostgresFixture postgres, TimeProvider? clock = null, Action<IServiceCollection>? replaceServices = null) =>
        new(postgres, clock, replaceServices);

    public ValueTask DisposeAsync() => factory?.DisposeAsync() ?? ValueTask.CompletedTask;
}

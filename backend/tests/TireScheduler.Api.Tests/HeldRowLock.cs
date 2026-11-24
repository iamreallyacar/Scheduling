using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Database;

namespace TireScheduler.Api.Tests;

// Holds a row lock in a transaction of its own, as a concurrent command would, so a lock test can
// check that a write waits for the row. A race between two requests passes by luck when the lock
// is missing; a held row does not (AC-211). A write that changes the held row also waits without
// taking the lock first, only later, after its checks: its test changes what the checks read
// while the row is held (whileHeld) and expects the write to see it.
internal sealed class HeldRowLock : IAsyncDisposable
{
    // Long enough that a write which takes no lock has answered by then, and that one which only
    // waits at its update has run its checks. On a much slower machine a missing lock could go
    // unnoticed, but a present one never fails the test.
    private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);

    private readonly AsyncServiceScope scope;
    private readonly OpenTransaction transaction;

    private HeldRowLock(AsyncServiceScope scope, OpenTransaction transaction)
    {
        this.scope = scope;
        this.transaction = transaction;
    }

    public static async Task<HeldRowLock> TakeAsync<TContext, TEntity>(SchedulerApiFactory factory, int id)
        where TContext : DbContext
        where TEntity : class
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        HeldRowLock? held = null;
        try
        {
            TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
            held = new(scope, await scope.ServiceProvider.GetRequiredService<RequestTransaction>().BeginAsync(context, cancellationToken));
            Assert.True(await context.LockRowAsync<TEntity>(id, cancellationToken), $"No {typeof(TEntity).Name} row has id {id}, so none is held.");
            return held;
        }
        catch
        {
            // An open transaction left behind would hold its connection, and maybe the row, for the rest of the run.
            if (held is null)
            {
                await scope.DisposeAsync();
            }
            else
            {
                await held.DisposeAsync();
            }

            throw;
        }
    }

    // Changes rows in the holding transaction, as the holding command would before it commits: for
    // example deleting the held row, or adding a row that refers to it. A write sent after this sees
    // the change only if it reads after taking the lock, so a test can tell a lock taken before the
    // checks from a wait at the write's own delete or foreign key, which comes after them.
    public async Task ChangeWhileHeldAsync<TContext>(Func<TContext, CancellationToken, Task> change)
        where TContext : DbContext
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        scope.ServiceProvider.GetRequiredService<RequestTransaction>().Join(context);
        await change(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    // Fails when the write answers while the row is held; otherwise does whileHeld, as the holding
    // command would, and releases the row.
    public async Task ReleaseOnceItWaitsAsync(Task write, Action? whileHeld = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Task first = await Task.WhenAny(write, Task.Delay(WaitTime, cancellationToken));
        Assert.False(ReferenceEquals(write, first), "The write answered while the row was held, so it does not lock that row.");
        whileHeld?.Invoke();
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await scope.DisposeAsync();
    }
}

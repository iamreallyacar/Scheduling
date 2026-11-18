using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace TireScheduler.Api.Database;

// A transaction begun through RequestTransaction. Ending it without a commit rolls back the rows
// of every module that joined it, and leaves those modules free to work on their own again.
public sealed class OpenTransaction : IAsyncDisposable
{
    private readonly IDbContextTransaction transaction;
    private readonly Action onEnded;
    private readonly List<DbContext> joinedContexts = [];

    internal OpenTransaction(IDbContextTransaction transaction, Action onEnded)
    {
        this.transaction = transaction;
        this.onEnded = onEnded;
    }

    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        foreach (DbContext context in joinedContexts)
        {
            await context.Database.UseTransactionAsync(null);
        }

        await transaction.DisposeAsync();
        onEnded();
    }

    internal void Enlist(DbContext context)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            // The context that began the transaction, or one that has joined already.
            return;
        }

        context.Database.UseTransaction(transaction.GetDbTransaction());
        joinedContexts.Add(context);
    }
}

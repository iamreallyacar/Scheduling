using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace TireScheduler.Api.Database;

// The one database transaction of a request (DEC-33). Every module context of a request runs on
// the same connection, so a module that a command calls through its public interface or a port
// joins the transaction the command began, and the rows of both commit or roll back together.
// A transaction still open when the request ends is rolled back.
public sealed class RequestTransaction : IAsyncDisposable
{
    private OpenTransaction? openTransaction;

    public async Task<OpenTransaction> BeginAsync(DbContext beginningContext, CancellationToken cancellationToken)
    {
        if (openTransaction is not null)
        {
            throw new InvalidOperationException(
                "This request already has an open transaction. A command begins one; the modules it calls join it.");
        }

        IDbContextTransaction transaction = await beginningContext.Database.BeginTransactionAsync(cancellationToken);
        openTransaction = new OpenTransaction(transaction, onEnded: () => openTransaction = null);
        return openTransaction;
    }

    public void Join(DbContext joiningContext)
    {
        OpenTransaction transaction = openTransaction ?? throw new InvalidOperationException(
            $"{joiningContext.GetType().Name} cannot join: no transaction is open in this request. The command must begin one first.");
        transaction.Enlist(joiningContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (openTransaction is not null)
        {
            await openTransaction.DisposeAsync();
        }
    }
}

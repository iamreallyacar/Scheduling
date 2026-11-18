using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace TireScheduler.Api.Database;

// Row locks (`SELECT ... FOR UPDATE`) whose schema, table and key column come from the EF model,
// so a rename in a migration cannot leave a stale name behind (ARCHITECTURE.md section "Persistence rules").
public static class RowLock
{
    // Holds the row until the open transaction ends; a second command that locks it waits until
    // then. Global query filters do not apply: a soft-deleted row is locked too. Returns false
    // when no row has this id.
    public static async Task<bool> LockRowAsync<TEntity>(this DbContext context, int id, CancellationToken cancellationToken)
        where TEntity : class
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"Locking a {typeof(TEntity).Name} row needs an open transaction; outside one the lock ends at once.");
        }

        List<int> lockedIds = await context.Database
            .SqlQueryRaw<int>(LockRowSql(context, typeof(TEntity)), id)
            .ToListAsync(cancellationToken);
        return lockedIds.Count > 0;
    }

    private static string LockRowSql(DbContext context, Type entityClrType)
    {
        IEntityType entityType = context.Model.FindEntityType(entityClrType)
            ?? throw new InvalidOperationException($"{entityClrType.Name} is not part of {context.GetType().Name}.");
        StoreObjectIdentifier table = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
            ?? throw new InvalidOperationException($"{entityClrType.Name} is not stored in a table.");
        IProperty key = entityType.FindPrimaryKey() is { Properties: [IProperty onlyKeyProperty] } && onlyKeyProperty.ClrType == typeof(int)
            ? onlyKeyProperty
            : throw new InvalidOperationException($"{entityClrType.Name} does not have a single int key, so its rows cannot be locked by id.");
        string keyColumnName = key.GetColumnName(table)
            ?? throw new InvalidOperationException($"The key of {entityClrType.Name} has no column in {table.Name}.");

        ISqlGenerationHelper sql = context.GetService<ISqlGenerationHelper>();
        string keyColumn = sql.DelimitIdentifier(keyColumnName);
        // SqlQueryRaw<int> reads a scalar result from a column named "Value".
        return $"SELECT {keyColumn} AS {sql.DelimitIdentifier("Value")} FROM {sql.DelimitIdentifier(table.Name, table.Schema)} "
            + $"WHERE {keyColumn} = {{0}} FOR UPDATE";
    }
}

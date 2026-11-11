using Microsoft.EntityFrameworkCore;
using Npgsql;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Database;

public static class UniqueConstraintSaving
{
    // Saves and lets the database decide uniqueness, so two requests at the same moment cannot
    // both save the same value. A broken unique index becomes 409 conflict.duplicate naming the
    // field (AC-24). `fieldByIndexName` maps each unique index of the saved tables to its field.
    public static async Task SaveChangesRejectingDuplicatesAsync(
        this DbContext context,
        IReadOnlyDictionary<string, string> fieldByIndexName,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: string indexName }
                && fieldByIndexName.TryGetValue(indexName, out string? field))
        {
            throw ApiProblemException.Duplicate(field, $"Another item already uses this {field}.");
        }
    }
}

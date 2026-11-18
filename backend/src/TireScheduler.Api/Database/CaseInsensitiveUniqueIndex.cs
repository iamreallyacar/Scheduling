using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TireScheduler.Api.Database;

public static class CaseInsensitiveUniqueIndex
{
    // "ps-205" and "PS-205" are the same value (A20). The database keeps an upper-case copy of
    // the column and the unique index is on that copy (ARCHITECTURE.md section "Persistence rules").
    public static IndexBuilder<TEntity> HasUniqueIndexWithoutRegardToCase<TEntity>(
        this EntityTypeBuilder<TEntity> entity,
        Expression<Func<TEntity, string>> textProperty,
        string indexName)
        where TEntity : class
    {
        string columnName = entity.Property(textProperty).Metadata.Name;
        string upperCaseColumnName = $"{columnName}UpperCase";
        entity.Property<string>(upperCaseColumnName).HasComputedColumnSql($"upper(\"{columnName}\")", stored: true);
        return entity.HasIndex(upperCaseColumnName).IsUnique().HasDatabaseName(indexName);
    }
}

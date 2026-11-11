using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TireScheduler.Api.Modules.MasterData.Calendars;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.MasterData.Products;

namespace TireScheduler.Api.Modules.MasterData;

internal sealed class MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : DbContext(options)
{
    public const string Schema = "master_data";

    // Names of the unique indexes, used to tell the client which field is a duplicate (AC-24).
    public const string ResourceTypeNameIndex = "UX_ResourceTypes_Name";
    public const string ResourceNameIndex = "UX_Resources_Name";
    public const string ProductCodeIndex = "UX_Products_Code";

    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<Calendar> Calendars => Set<Calendar>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ResourceType>(resourceType =>
        {
            resourceType.Property(t => t.Name).HasMaxLength(ResourceType.NameMaxLength);
            resourceType.Property(t => t.Kind).HasConversion<string>().HasMaxLength(20);
            UniqueWithoutRegardToCase(resourceType, t => t.Name, ResourceTypeNameIndex);
        });

        modelBuilder.Entity<Resource>(resource =>
        {
            resource.Property(r => r.Name).HasMaxLength(Resource.NameMaxLength);
            UniqueWithoutRegardToCase(resource, r => r.Name, ResourceNameIndex);
            // Restrict: a type or calendar that a machine references cannot be deleted (A14).
            resource.HasOne(r => r.ResourceType).WithMany().HasForeignKey(r => r.ResourceTypeId).OnDelete(DeleteBehavior.Restrict);
            resource.HasOne(r => r.Calendar).WithMany().HasForeignKey(r => r.CalendarId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Calendar>(calendar =>
        {
            calendar.Property(c => c.Name).HasMaxLength(Calendar.NameMaxLength);
            // At most one calendar is the plant default.
            calendar.HasIndex(c => c.IsPlantDefault).IsUnique().HasFilter("\"IsPlantDefault\"");
            calendar.HasMany(c => c.Shifts).WithOne().HasForeignKey(s => s.CalendarId);
        });

        modelBuilder.Entity<CalendarShift>(shift =>
        {
            shift.HasKey(s => new { s.CalendarId, s.Weekday, s.StartMinuteOfDay });
            shift.Property(s => s.Weekday).HasConversion<string>().HasMaxLength(10);
            shift.ToTable("CalendarShifts", table => table.HasCheckConstraint(
                "CK_CalendarShifts_WithinOneDay",
                $"\"StartMinuteOfDay\" >= 0 AND \"StartMinuteOfDay\" < \"EndMinuteOfDay\" AND \"EndMinuteOfDay\" <= {CalendarShift.MinutesPerDay}"));
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Code).HasMaxLength(Product.CodeMaxLength);
            product.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
            product.Property(p => p.Spec).HasMaxLength(Product.SpecMaxLength);
            UniqueWithoutRegardToCase(product, p => p.Code, ProductCodeIndex);
        });
    }

    // "ps-205" and "PS-205" are the same value (AC-31, A20). The database keeps an upper-case
    // copy of the column and the unique index is on that copy.
    private static void UniqueWithoutRegardToCase<TEntity>(
        EntityTypeBuilder<TEntity> entity,
        Expression<Func<TEntity, string>> textProperty,
        string indexName)
        where TEntity : class
    {
        string columnName = entity.Property(textProperty).Metadata.Name;
        string upperCaseColumnName = $"{columnName}UpperCase";
        entity.Property<string>(upperCaseColumnName).HasComputedColumnSql($"upper(\"{columnName}\")", stored: true);
        entity.HasIndex(upperCaseColumnName).IsUnique().HasDatabaseName(indexName);
    }
}

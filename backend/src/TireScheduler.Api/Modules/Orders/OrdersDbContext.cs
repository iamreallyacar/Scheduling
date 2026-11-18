using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;

namespace TireScheduler.Api.Modules.Orders;

internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string Schema = "orders";

    // Name of the unique index, used to tell the client which field is a duplicate (AC-24).
    public const string OrderNoIndex = "UX_Orders_OrderNo";

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<OperationDependency> OperationDependencies => Set<OperationDependency>();

    public DbSet<Lot> Lots => Set<Lot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Order>(order =>
        {
            order.Property(o => o.OrderNo).HasMaxLength(Order.OrderNoMaxLength);
            order.Property(o => o.CustomerName).HasMaxLength(Order.CustomerNameMaxLength);
            order.Property(o => o.Priority).HasConversion<string>().HasMaxLength(10);
            // A deleted order frees its number (A25).
            order.HasUniqueIndexWithoutRegardToCase(o => o.OrderNo, OrderNoIndex).HasFilter("\"IsDeleted\" = false");
            order.HasQueryFilter(o => !o.IsDeleted);
            order.HasMany(o => o.Operations).WithOne().HasForeignKey(operation => operation.OrderId).OnDelete(DeleteBehavior.Cascade);
            order.HasMany(o => o.Dependencies).WithOne().HasForeignKey(dependency => dependency.OrderId).OnDelete(DeleteBehavior.Cascade);
            order.ToTable("Orders", table => table.HasCheckConstraint("CK_Orders_Quantity", "\"Quantity\" >= 1"));
        });

        modelBuilder.Entity<Operation>(operation =>
        {
            operation.HasIndex(o => new { o.OrderId, o.SequenceNo }).IsUnique();
            operation.HasMany(o => o.Lots).WithOne().HasForeignKey(lot => lot.OperationId).OnDelete(DeleteBehavior.Cascade);
            operation.ToTable("Operations", table => table.HasCheckConstraint(
                "CK_Operations_Values",
                "\"SequenceNo\" >= 1 AND \"Quantity\" >= 1 AND \"SetupMinutes\" >= 0 AND \"CycleMinutes\" > 0 AND \"UnitsPerCycle\" >= 1"));
        });

        modelBuilder.Entity<Lot>(lot =>
        {
            lot.HasIndex(l => new { l.OperationId, l.LotNo }).IsUnique();
            lot.ToTable("Lots", table => table.HasCheckConstraint(
                "CK_Lots_Values",
                "\"LotNo\" >= 1 AND \"Quantity\" >= 1 AND \"DurationMinutes\" > 0"));
        });

        modelBuilder.Entity<OperationDependency>(dependency =>
        {
            dependency.HasKey(d => new { d.PredecessorOperationId, d.SuccessorOperationId });
            dependency.HasOne(d => d.PredecessorOperation).WithMany().HasForeignKey(d => d.PredecessorOperationId).OnDelete(DeleteBehavior.Cascade);
            dependency.HasOne(d => d.SuccessorOperation).WithMany().HasForeignKey(d => d.SuccessorOperationId).OnDelete(DeleteBehavior.Cascade);
            dependency.ToTable("OperationDependencies", table => table.HasCheckConstraint(
                "CK_OperationDependencies_LagAndOperations",
                "\"MinLagMinutes\" >= 0 AND \"PredecessorOperationId\" <> \"SuccessorOperationId\""));
        });
    }
}

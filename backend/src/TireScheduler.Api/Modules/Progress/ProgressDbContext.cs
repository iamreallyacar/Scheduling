using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Progress;

internal sealed class ProgressDbContext(DbContextOptions<ProgressDbContext> options) : DbContext(options)
{
    public const string Schema = "progress";

    public DbSet<LotProgress> LotProgress => Set<LotProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // One row per started lot, keyed by the lot id that Orders gave it.
        modelBuilder.Entity<LotProgress>(progress =>
        {
            progress.HasKey(p => p.LotId);
            progress.Property(p => p.LotId).ValueGeneratedNever();
            progress.ToTable("LotProgress", table =>
                table.HasCheckConstraint("CK_LotProgress_EndAfterStart", "\"ActualEnd\" IS NULL OR \"ActualEnd\" > \"ActualStart\""));
        });
    }
}

using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options)
{
    public const string Schema = "scheduling";

    public DbSet<Unavailability> Unavailabilities => Set<Unavailability>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Unavailability>(unavailability =>
        {
            unavailability.Property(u => u.Kind).HasConversion<string>().HasMaxLength(20);
            unavailability.Property(u => u.Reason).HasMaxLength(Unavailability.ReasonMaxLength);
            unavailability.HasIndex(u => new { u.ResourceId, u.StartsAt });
            unavailability.ToTable("Unavailabilities", table =>
            {
                table.HasCheckConstraint("CK_Unavailabilities_EndAfterStart", "\"EndsAt\" IS NULL OR \"EndsAt\" > \"StartsAt\"");
                // Only a breakdown can have no known end (domain-model.md section 2).
                table.HasCheckConstraint("CK_Unavailabilities_OpenEndOnlyForBreakdown", "\"EndsAt\" IS NOT NULL OR \"Kind\" = 'Breakdown'");
            });
        });
    }
}

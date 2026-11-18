using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options)
{
    public const string Schema = "scheduling";

    public DbSet<Unavailability> Unavailabilities => Set<Unavailability>();

    public DbSet<ScheduleVersion> ScheduleVersions => Set<ScheduleVersion>();

    public DbSet<Placement> Placements => Set<Placement>();

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

        modelBuilder.Entity<ScheduleVersion>(version =>
        {
            version.Property(v => v.Name).HasMaxLength(ScheduleVersion.NameMaxLength);
            version.HasIndex(v => v.IsLive).IsUnique().HasFilter("\"IsLive\"");
        });

        // No constraint keeps placements apart: recomputed ends may overlap, and the resource row
        // lock keeps R2 across requests (DEC-43).
        modelBuilder.Entity<Placement>(placement =>
        {
            placement.HasOne<ScheduleVersion>().WithMany().HasForeignKey(p => p.VersionId).OnDelete(DeleteBehavior.Cascade);
            placement.HasIndex(p => new { p.VersionId, p.LotId }).IsUnique();
            placement.HasIndex(p => p.LotId);
            placement.HasIndex(p => p.ResourceId);
            placement.ToTable("Placements", table =>
                table.HasCheckConstraint("CK_Placements_EndAfterStart", "\"PlannedEnd\" > \"PlannedStart\""));
        });
    }
}

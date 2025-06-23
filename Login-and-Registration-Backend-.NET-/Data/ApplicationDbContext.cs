using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Data
{	public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{
		}

		// Add DbSets for production entities
		public DbSet<ProductionOrder> ProductionOrders { get; set; }
		public DbSet<ProductionJob> ProductionJobs { get; set; }
		public DbSet<Machine> Machines { get; set; }

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			// Add any additional model configurations here
			builder.Entity<ApplicationUser>().HasIndex(u => u.UserName).IsUnique();
			builder.Entity<ApplicationUser>().HasIndex(u => u.Email).IsUnique();

			// Configure production entities
			builder.Entity<ProductionOrder>(entity =>
			{
				entity.HasIndex(e => e.OrderNumber).IsUnique();
				entity.Property(e => e.EstimatedHours).HasPrecision(10, 2);
			});

			builder.Entity<ProductionJob>(entity =>
			{
				entity.Property(e => e.Duration).HasPrecision(10, 2);
				entity.HasOne(e => e.ProductionOrder)
					.WithMany(e => e.ProductionJobs)
					.HasForeignKey(e => e.ProductionOrderId)
					.OnDelete(DeleteBehavior.Cascade);
				
				entity.HasOne(e => e.Machine)
					.WithMany(e => e.ProductionJobs)
					.HasForeignKey(e => e.MachineId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			builder.Entity<Machine>(entity =>
			{
				entity.HasIndex(e => e.Name).IsUnique();
			});
	}
	}
}
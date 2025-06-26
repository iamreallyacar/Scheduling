using Microsoft.EntityFrameworkCore;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Services
{
    /// <summary>
    /// Service for seeding the database with initial data
    /// </summary>
    public class DatabaseSeedingService : IDatabaseSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseSeedingService> _logger;

        public DatabaseSeedingService(
            ApplicationDbContext context,
            ILogger<DatabaseSeedingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Seeds the database with all initial data
        /// </summary>
        public async Task SeedDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting database seeding process");

                // Apply migrations first
                await MigrateDatabaseAsync(cancellationToken);

                // Seed tire production machines
                await SeedTireProductionMachinesAsync(cancellationToken);

                // Seed production orders (if needed)
                await SeedProductionOrdersAsync(cancellationToken);

                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }

        /// <summary>
        /// Applies pending migrations to the database
        /// </summary>
        public async Task MigrateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if the provider supports migrations (skip for InMemory database used in tests)
                if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                {
                    _logger.LogInformation("Using InMemory database provider, skipping migrations");
                    return;
                }

                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
                
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                    await _context.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Database migrations applied successfully");
                }
                else
                {
                    _logger.LogInformation("No pending migrations found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database migration");
                throw;
            }
        }

        /// <summary>
        /// Seeds tire production machines if they don't exist
        /// </summary>
        public async Task SeedTireProductionMachinesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _context.Machines.AnyAsync(cancellationToken))
                {
                    _logger.LogInformation("Machines already exist in database, skipping seeding");
                    return;
                }

                _logger.LogInformation("Seeding tire production machines");

                var tireMachines = new[]
                {
                    new Machine
                    {
                        Name = "Tire Molding Press 1",
                        Type = "Molding Press",
                        Status = MachineStatus.Idle,
                        Utilization = 0,
                        IsActive = true,
                        LastMaintenance = DateTime.UtcNow.AddDays(-30),
                        NextMaintenance = DateTime.UtcNow.AddDays(30),
                        Notes = "Primary tire molding press for passenger car tires"
                    },
                    new Machine
                    {
                        Name = "Tire Molding Press 2",
                        Type = "Molding Press",
                        Status = MachineStatus.Idle,
                        Utilization = 0,
                        IsActive = true,
                        LastMaintenance = DateTime.UtcNow.AddDays(-25),
                        NextMaintenance = DateTime.UtcNow.AddDays(35),
                        Notes = "Secondary tire molding press for high-performance tires"
                    },
                    new Machine
                    {
                        Name = "Tire Building Machine 1",
                        Type = "Building Machine",
                        Status = MachineStatus.Idle,
                        Utilization = 0,
                        IsActive = true,
                        LastMaintenance = DateTime.UtcNow.AddDays(-20),
                        NextMaintenance = DateTime.UtcNow.AddDays(40),
                        Notes = "Automated tire building for consistent quality"
                    },
                    new Machine
                    {
                        Name = "Tread Extrusion Line 1",
                        Type = "Extrusion Line",
                        Status = MachineStatus.Idle,
                        Utilization = 0,
                        IsActive = true,
                        LastMaintenance = DateTime.UtcNow.AddDays(-15),
                        NextMaintenance = DateTime.UtcNow.AddDays(45),
                        Notes = "High-capacity tread extrusion for various tire sizes"
                    },
                    new Machine
                    {
                        Name = "Quality Control Station",
                        Type = "QC Station",
                        Status = MachineStatus.Idle,
                        Utilization = 0,
                        IsActive = true,
                        LastMaintenance = DateTime.UtcNow.AddDays(-10),
                        NextMaintenance = DateTime.UtcNow.AddDays(50),
                        Notes = "Final quality inspection and testing station"
                    }
                };

                await _context.Machines.AddRangeAsync(tireMachines, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully seeded {Count} tire production machines", tireMachines.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding tire production machines");
                throw;
            }
        }

        /// <summary>
        /// Seeds production orders if they don't exist
        /// </summary>
        public async Task SeedProductionOrdersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _context.ProductionOrders.AnyAsync(cancellationToken))
                {
                    _logger.LogInformation("Production orders already exist in database, skipping seeding");
                    return;
                }

                _logger.LogInformation("Seeding sample production orders");

                var sampleOrders = new[]
                {
                    new ProductionOrder
                    {
                        OrderNumber = "PO-2025-001",
                        CustomerName = "AutoCorp Manufacturing",
                        ProductName = "Premium All-Season Tire 205/55R16",
                        Quantity = 1000,
                        Priority = Priority.High,
                        EstimatedHours = 48.50m,
                        Status = OrderStatus.Pending,
                        DueDate = DateTime.UtcNow.AddDays(7),
                        CreatedDate = DateTime.UtcNow,
                        Notes = "High-priority order for major automotive manufacturer"
                    },
                    new ProductionOrder
                    {
                        OrderNumber = "PO-2025-002",
                        CustomerName = "WinterTech Industries",
                        ProductName = "Performance Winter Tire 225/45R17",
                        Quantity = 750,
                        Priority = Priority.Medium,
                        EstimatedHours = 36.25m,
                        Status = OrderStatus.Pending,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        CreatedDate = DateTime.UtcNow,
                        Notes = "Winter tire production for seasonal demand"
                    },
                    new ProductionOrder
                    {
                        OrderNumber = "PO-2025-003",
                        CustomerName = "TruckFleet Solutions",
                        ProductName = "Commercial Truck Tire 275/70R22.5",
                        Quantity = 500,
                        Priority = Priority.Low,
                        EstimatedHours = 72.00m,
                        Status = OrderStatus.Pending,
                        DueDate = DateTime.UtcNow.AddDays(21),
                        CreatedDate = DateTime.UtcNow,
                        Notes = "Heavy-duty commercial tire production"
                    }
                };

                await _context.ProductionOrders.AddRangeAsync(sampleOrders, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully seeded {Count} sample production orders", sampleOrders.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding production orders");
                throw;
            }
        }
    }
}

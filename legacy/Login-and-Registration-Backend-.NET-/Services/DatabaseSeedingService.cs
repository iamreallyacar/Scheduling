using Microsoft.EntityFrameworkCore;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;
using Login_and_Registration_Backend_.NET_.Configuration;
using System.Text.Json;

namespace Login_and_Registration_Backend_.NET_.Services
{
    /// <summary>
    /// Service for seeding the database with initial data
    /// </summary>
    public class DatabaseSeedingService : IDatabaseSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseSeedingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public DatabaseSeedingService(
            ApplicationDbContext context,
            ILogger<DatabaseSeedingService> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        /// <summary>
        /// Seeds the database with all initial data
        /// </summary>
        public async Task SeedDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting database seeding process for environment: {Environment}", _environment.EnvironmentName);

                // Apply migrations first
                await MigrateDatabaseAsync(cancellationToken);

                // Use environment-specific seeding
                await SeedForEnvironmentAsync(_environment.EnvironmentName, cancellationToken);

                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }

        /// <summary>
        /// Seeds the database with environment-specific data
        /// </summary>
        public async Task SeedForEnvironmentAsync(string environment, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Seeding data for environment: {Environment}", environment);

                var seedConfig = await LoadSeedConfigurationAsync(environment);
                
                if (seedConfig == null)
                {
                    _logger.LogWarning("No seed configuration found for environment: {Environment}. Using fallback seeding.", environment);
                    await FallbackSeedAsync(cancellationToken);
                    return;
                }

                // Clean existing data if enabled
                if (seedConfig.EnableCleanup)
                {
                    await CleanSeedDataAsync(cancellationToken);
                }

                // Seed machines
                await SeedMachinesFromConfigAsync(seedConfig.Machines, cancellationToken);

                // Seed orders
                await SeedOrdersFromConfigAsync(seedConfig.Orders, cancellationToken);

                // Seed relationships (jobs) if enabled
                if (seedConfig.SeedRelationships)
                {
                    await SeedJobsFromConfigAsync(seedConfig.Jobs, cancellationToken);
                }

                _logger.LogInformation("Environment-specific seeding completed for: {Environment}", environment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during environment-specific seeding for: {Environment}", environment);
                throw;
            }
        }

        /// <summary>
        /// Loads seed configuration from environment-specific JSON file
        /// </summary>
        private async Task<SeedConfiguration?> LoadSeedConfigurationAsync(string environment)
        {
            try
            {
                var configFileName = $"seeddata-{environment.ToLower()}.json";
                var configPath = Path.Combine(_environment.ContentRootPath, "Configuration", configFileName);

                if (!File.Exists(configPath))
                {
                    _logger.LogWarning("Seed configuration file not found: {ConfigPath}", configPath);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(configPath);
                var configRoot = JsonSerializer.Deserialize<Dictionary<string, SeedConfiguration>>(jsonContent);
                
                return configRoot?.GetValueOrDefault("SeedConfiguration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading seed configuration for environment: {Environment}", environment);
                return null;
            }
        }

        /// <summary>
        /// Seeds machines from configuration
        /// </summary>
        private async Task SeedMachinesFromConfigAsync(List<MachineConfig> machineConfigs, CancellationToken cancellationToken)
        {
            if (!machineConfigs.Any())
            {
                _logger.LogInformation("No machine configurations found, skipping machine seeding");
                return;
            }

            // Check if machines already exist (by name)
            var existingMachineNames = await _context.Machines
                .Where(m => !m.IsDeleted)
                .Select(m => m.Name)
                .ToListAsync(cancellationToken);

            var machinesToSeed = machineConfigs
                .Where(config => !existingMachineNames.Contains(config.Name))
                .ToList();

            if (!machinesToSeed.Any())
            {
                _logger.LogInformation("All configured machines already exist, skipping machine seeding");
                return;
            }

            _logger.LogInformation("Seeding {Count} machines from configuration", machinesToSeed.Count);

            var machines = machinesToSeed.Select(config => new Machine
            {
                Name = config.Name,
                Type = config.Type,
                Status = Enum.Parse<MachineStatus>(config.Status, true),
                Utilization = config.Utilization,
                IsActive = config.IsActive,
                LastMaintenance = DateTime.UtcNow.AddDays(-config.LastMaintenanceDaysAgo),
                NextMaintenance = DateTime.UtcNow.AddDays(config.NextMaintenanceDaysAhead),
                Notes = config.Notes,
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            }).ToArray();

            await _context.Machines.AddRangeAsync(machines, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully seeded {Count} machines", machines.Length);
        }

        /// <summary>
        /// Seeds production orders from configuration
        /// </summary>
        private async Task SeedOrdersFromConfigAsync(List<OrderConfig> orderConfigs, CancellationToken cancellationToken)
        {
            if (!orderConfigs.Any())
            {
                _logger.LogInformation("No order configurations found, skipping order seeding");
                return;
            }

            // Check if orders already exist (by order number)
            var existingOrderNumbers = await _context.ProductionOrders
                .Where(o => !o.IsDeleted)
                .Select(o => o.OrderNumber)
                .ToListAsync(cancellationToken);

            var ordersToSeed = orderConfigs
                .Where(config => !existingOrderNumbers.Contains(config.OrderNumber))
                .ToList();

            if (!ordersToSeed.Any())
            {
                _logger.LogInformation("All configured orders already exist, skipping order seeding");
                return;
            }

            _logger.LogInformation("Seeding {Count} production orders from configuration", ordersToSeed.Count);

            var orders = ordersToSeed.Select(config => new ProductionOrder
            {
                OrderNumber = config.OrderNumber,
                CustomerName = config.CustomerName,
                ProductName = config.ProductName,
                Quantity = config.Quantity,
                Priority = Enum.Parse<Priority>(config.Priority, true),
                EstimatedHours = config.EstimatedHours,
                Status = Enum.Parse<OrderStatus>(config.Status, true),
                DueDate = DateTime.UtcNow.AddDays(config.DueDateDaysAhead),
                Notes = config.Notes,
                CreatedBy = config.CreatedBy,
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            }).ToArray();

            await _context.ProductionOrders.AddRangeAsync(orders, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully seeded {Count} production orders", orders.Length);
        }

        /// <summary>
        /// Seeds production jobs from configuration
        /// </summary>
        private async Task SeedJobsFromConfigAsync(List<JobConfig> jobConfigs, CancellationToken cancellationToken)
        {
            if (!jobConfigs.Any())
            {
                _logger.LogInformation("No job configurations found, skipping job seeding");
                return;
            }

            _logger.LogInformation("Seeding {Count} production jobs from configuration", jobConfigs.Count);

            // Get all machines and orders for mapping
            var machines = await _context.Machines
                .Where(m => !m.IsDeleted)
                .ToDictionaryAsync(m => m.Name, m => m.Id, cancellationToken);

            var orders = await _context.ProductionOrders
                .Where(o => !o.IsDeleted)
                .ToDictionaryAsync(o => o.OrderNumber, o => o.Id, cancellationToken);

            var validJobs = new List<ProductionJob>();

            foreach (var config in jobConfigs)
            {
                if (!machines.TryGetValue(config.MachineName, out var machineId))
                {
                    _logger.LogWarning("Machine not found for job: {JobName}, Machine: {MachineName}", config.JobName, config.MachineName);
                    continue;
                }

                if (!orders.TryGetValue(config.OrderNumber, out var orderId))
                {
                    _logger.LogWarning("Order not found for job: {JobName}, Order: {OrderNumber}", config.JobName, config.OrderNumber);
                    continue;
                }

                var scheduledStart = DateTime.UtcNow.AddDays(config.ScheduledStartDaysAhead);
                var scheduledEnd = scheduledStart.AddHours((double)config.Duration);

                validJobs.Add(new ProductionJob
                {
                    JobName = config.JobName,
                    ProductionOrderId = orderId,
                    MachineId = machineId,
                    Duration = config.Duration,
                    Status = Enum.Parse<JobStatus>(config.Status, true),
                    ScheduledStartTime = scheduledStart,
                    ScheduledEndTime = scheduledEnd,
                    Operator = config.Operator,
                    Notes = config.Notes,
                    SortOrder = config.SortOrder,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false
                });
            }

            if (validJobs.Any())
            {
                await _context.ProductionJobs.AddRangeAsync(validJobs, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully seeded {Count} production jobs", validJobs.Count);
            }
        }

        /// <summary>
        /// Cleans all seed data from the database
        /// </summary>
        public async Task CleanSeedDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting cleanup of seed data");

                // Soft delete production jobs
                var jobs = await _context.ProductionJobs
                    .Where(j => !j.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var job in jobs)
                {
                    job.IsDeleted = true;
                    job.DeletedDate = DateTime.UtcNow;
                }

                // Soft delete production orders
                var orders = await _context.ProductionOrders
                    .Where(o => !o.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var order in orders)
                {
                    order.IsDeleted = true;
                    order.DeletedDate = DateTime.UtcNow;
                }

                // Soft delete machines
                var machines = await _context.Machines
                    .Where(m => !m.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var machine in machines)
                {
                    machine.IsDeleted = true;
                    machine.DeletedDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Cleanup completed. Soft deleted {JobCount} jobs, {OrderCount} orders, {MachineCount} machines", 
                    jobs.Count, orders.Count, machines.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during seed data cleanup");
                throw;
            }
        }

        /// <summary>
        /// Fallback seeding method using hardcoded data (original implementation)
        /// </summary>
        private async Task FallbackSeedAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Using fallback seeding with hardcoded data");
            
            // Use original hardcoded seeding methods
            await SeedTireProductionMachinesAsync(cancellationToken);
            await SeedProductionOrdersAsync(cancellationToken);
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
        /// Seeds tire production machines if they don't exist (fallback method)
        /// </summary>
        public async Task SeedTireProductionMachinesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _context.Machines.AnyAsync(m => !m.IsDeleted, cancellationToken))
                {
                    _logger.LogInformation("Machines already exist in database, skipping seeding");
                    return;
                }

                _logger.LogInformation("Seeding tire production machines (fallback)");

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
                        Notes = "Primary tire molding press for passenger car tires",
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
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
                        Notes = "Secondary tire molding press for high-performance tires",
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
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
                        Notes = "Automated tire building for consistent quality",
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
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
                        Notes = "High-capacity tread extrusion for various tire sizes",
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
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
                        Notes = "Final quality inspection and testing station",
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
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
        /// Seeds production orders if they don't exist (fallback method)
        /// </summary>
        public async Task SeedProductionOrdersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _context.ProductionOrders.AnyAsync(o => !o.IsDeleted, cancellationToken))
                {
                    _logger.LogInformation("Production orders already exist in database, skipping seeding");
                    return;
                }

                _logger.LogInformation("Seeding sample production orders (fallback)");

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
                        Notes = "High-priority order for major automotive manufacturer",
                        IsDeleted = false
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
                        Notes = "Winter tire production for seasonal demand",
                        IsDeleted = false
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
                        Notes = "Heavy-duty commercial tire production",
                        IsDeleted = false
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

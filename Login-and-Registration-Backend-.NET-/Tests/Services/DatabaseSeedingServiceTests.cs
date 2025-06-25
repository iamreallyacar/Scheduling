using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Services;
using Login_and_Registration_Backend_.NET_.Models;
using Xunit;

namespace Login_and_Registration_Backend_.NET_.Tests.Services
{
    public class DatabaseSeedingServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SeedTireProductionMachinesAsync_WhenNoMachinesExist_SeedsExpectedMachines()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<DatabaseSeedingService>>();
            var service = new DatabaseSeedingService(context, logger.Object);

            // Act
            await service.SeedTireProductionMachinesAsync();

            // Assert
            var machines = await context.Machines.ToListAsync();
            Assert.Equal(5, machines.Count);
            Assert.Contains(machines, m => m.Name == "Tire Molding Press 1");
            Assert.Contains(machines, m => m.Name == "Tire Molding Press 2");
            Assert.Contains(machines, m => m.Name == "Tire Building Machine 1");
            Assert.Contains(machines, m => m.Name == "Tread Extrusion Line 1");
            Assert.Contains(machines, m => m.Name == "Quality Control Station");

            // Verify all machines have expected properties
            Assert.All(machines, machine =>
            {
                Assert.True(machine.IsActive);
                Assert.Equal("idle", machine.Status);
                Assert.Equal(0, machine.Utilization);
                Assert.True(machine.LastMaintenance < DateTime.UtcNow);
                Assert.True(machine.NextMaintenance > DateTime.UtcNow);
            });
        }

        [Fact]
        public async Task SeedTireProductionMachinesAsync_WhenMachinesAlreadyExist_DoesNotSeedAgain()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<DatabaseSeedingService>>();
            var service = new DatabaseSeedingService(context, logger.Object);

            // Add an existing machine
            context.Machines.Add(new Machine { Name = "Existing Machine", Type = "Test", Status = "idle" });
            await context.SaveChangesAsync();

            // Act
            await service.SeedTireProductionMachinesAsync();

            // Assert
            var machines = await context.Machines.ToListAsync();
            Assert.Single(machines);
            Assert.Equal("Existing Machine", machines[0].Name);
        }

        [Fact]
        public async Task SeedProductionOrdersAsync_WhenNoOrdersExist_SeedsExpectedOrders()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<DatabaseSeedingService>>();
            var service = new DatabaseSeedingService(context, logger.Object);

            // Act
            await service.SeedProductionOrdersAsync();

            // Assert
            var orders = await context.ProductionOrders.ToListAsync();
            Assert.Equal(3, orders.Count);
            Assert.Contains(orders, o => o.OrderNumber == "PO-2025-001");
            Assert.Contains(orders, o => o.OrderNumber == "PO-2025-002");
            Assert.Contains(orders, o => o.OrderNumber == "PO-2025-003");

            // Verify all orders have expected properties
            Assert.All(orders, order =>
            {
                Assert.NotEmpty(order.CustomerName);
                Assert.NotEmpty(order.ProductName);
                Assert.True(order.Quantity > 0);
                Assert.Equal("pending", order.Status);
                Assert.True(order.EstimatedHours > 0);
                Assert.True(order.DueDate > DateTime.UtcNow);
            });
        }

        [Fact]
        public async Task SeedProductionOrdersAsync_WhenOrdersAlreadyExist_DoesNotSeedAgain()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<DatabaseSeedingService>>();
            var service = new DatabaseSeedingService(context, logger.Object);

            // Add an existing order
            context.ProductionOrders.Add(new ProductionOrder 
            { 
                OrderNumber = "PO-EXISTING-001",
                CustomerName = "Test Customer",
                ProductName = "Test Product",
                Quantity = 100,
                DueDate = DateTime.UtcNow.AddDays(1)
            });
            await context.SaveChangesAsync();

            // Act
            await service.SeedProductionOrdersAsync();

            // Assert
            var orders = await context.ProductionOrders.ToListAsync();
            Assert.Single(orders);
            Assert.Equal("PO-EXISTING-001", orders[0].OrderNumber);
        }

        [Fact]
        public async Task SeedDatabaseAsync_SeedsAllDataTypes()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<DatabaseSeedingService>>();
            var service = new DatabaseSeedingService(context, logger.Object);

            // Act
            await service.SeedDatabaseAsync();

            // Assert
            var machines = await context.Machines.ToListAsync();
            var orders = await context.ProductionOrders.ToListAsync();
            
            Assert.Equal(5, machines.Count);
            Assert.Equal(3, orders.Count);
        }
    }
}

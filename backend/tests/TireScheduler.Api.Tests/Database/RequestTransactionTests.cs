using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Products;
using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Tests.Database;

public sealed class RequestTransactionTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AC_161_rows_of_two_modules_are_not_saved_when_the_command_ends_without_committing()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());

        await WriteProductAndOrderAsync(factory, "AC161-A", commit: false);

        Assert.False(await AnyProductOrOrderAsync(factory, "AC161-A"));
    }

    [Fact]
    public async Task AC_161_rows_of_two_modules_are_saved_together_when_the_command_commits()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());

        await WriteProductAndOrderAsync(factory, "AC161-B", commit: true);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Assert.True(await scope.ServiceProvider.GetRequiredService<MasterDataDbContext>().Products.AnyAsync(product => product.Code == "AC161-B", cancellationToken));
        Assert.True(await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Orders.AnyAsync(order => order.OrderNo == "AC161-B", cancellationToken));
    }

    [Fact]
    public async Task AC_161_a_module_cannot_join_when_no_transaction_is_open()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RequestTransaction requestTransaction = scope.ServiceProvider.GetRequiredService<RequestTransaction>();

        Assert.Throws<InvalidOperationException>(() => requestTransaction.Join(scope.ServiceProvider.GetRequiredService<OrdersDbContext>()));
    }

    [Fact]
    public async Task AC_161_a_second_transaction_cannot_begin_while_one_is_open()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RequestTransaction requestTransaction = scope.ServiceProvider.GetRequiredService<RequestTransaction>();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using OpenTransaction first = await requestTransaction.BeginAsync(scope.ServiceProvider.GetRequiredService<MasterDataDbContext>(), cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestTransaction.BeginAsync(scope.ServiceProvider.GetRequiredService<OrdersDbContext>(), cancellationToken));
    }

    [Fact]
    public async Task AC_161_a_joined_module_works_on_its_own_again_after_the_transaction_ends()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RequestTransaction requestTransaction = scope.ServiceProvider.GetRequiredService<RequestTransaction>();
        MasterDataDbContext masterData = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        OrdersDbContext orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using (OpenTransaction transaction = await requestTransaction.BeginAsync(masterData, cancellationToken))
        {
            requestTransaction.Join(orders);
        }

        Assert.Null(orders.Database.CurrentTransaction);
        await using OpenTransaction next = await requestTransaction.BeginAsync(orders, cancellationToken);
        Assert.NotNull(orders.Database.CurrentTransaction);
    }

    // MasterData begins the transaction and Orders joins it, as a module called through a port does.
    private static async Task WriteProductAndOrderAsync(SchedulerApiFactory factory, string code, bool commit)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RequestTransaction requestTransaction = scope.ServiceProvider.GetRequiredService<RequestTransaction>();
        MasterDataDbContext masterData = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        OrdersDbContext orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(masterData, cancellationToken);
        Product product = new() { Code = code, Name = $"Product {code}" };
        masterData.Products.Add(product);
        await masterData.SaveChangesAsync(cancellationToken);

        requestTransaction.Join(orders);
        orders.Orders.Add(new Order
        {
            OrderNo = code,
            CustomerName = "Customer",
            ProductId = product.Id,
            Quantity = 1,
            DueDate = new DateOnly(2026, 12, 1),
            Priority = OrderPriority.Normal,
            CreatedAt = CreatedAt,
        });
        await orders.SaveChangesAsync(cancellationToken);

        if (commit)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task<bool> AnyProductOrOrderAsync(SchedulerApiFactory factory, string code)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        return await scope.ServiceProvider.GetRequiredService<MasterDataDbContext>().Products.AnyAsync(product => product.Code == code, cancellationToken)
            || await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Orders.IgnoreQueryFilters().AnyAsync(order => order.OrderNo == code, cancellationToken);
    }
}

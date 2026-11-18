using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TireScheduler.Api.Database;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Products;

namespace TireScheduler.Api.Tests.Database;

public sealed class RowLockTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_162_a_locked_row_waits_until_the_command_that_holds_it_ends()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        int productId = await CreateProductAsync(factory, "AC162-A");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope holderScope = factory.Services.CreateAsyncScope();
        MasterDataDbContext holder = holderScope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        OpenTransaction holderTransaction = await holderScope.ServiceProvider.GetRequiredService<RequestTransaction>().BeginAsync(holder, cancellationToken);
        Assert.True(await holder.LockRowAsync<Product>(productId, cancellationToken));

        // EF reports the lock timeout as a transient failure around the PostgreSQL error.
        InvalidOperationException waitedTooLong = await Assert.ThrowsAsync<InvalidOperationException>(() => TryLockWithShortTimeoutAsync(factory, productId));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, Assert.IsType<PostgresException>(waitedTooLong.InnerException).SqlState);

        await holderTransaction.CommitAsync(cancellationToken);
        await holderTransaction.DisposeAsync();
        Assert.True(await TryLockWithShortTimeoutAsync(factory, productId));
    }

    [Fact]
    public async Task AC_162_a_lock_of_a_missing_row_finds_nothing()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MasterDataDbContext context = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using OpenTransaction transaction = await scope.ServiceProvider.GetRequiredService<RequestTransaction>().BeginAsync(context, cancellationToken);

        Assert.False(await context.LockRowAsync<Product>(999_999, cancellationToken));
    }

    [Fact]
    public async Task AC_162_a_lock_outside_a_transaction_fails()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        int productId = await CreateProductAsync(factory, "AC162-C");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MasterDataDbContext context = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.LockRowAsync<Product>(productId, TestContext.Current.CancellationToken));
    }

    // A second command that gives up after a short wait instead of waiting for the first to end.
    private static async Task<bool> TryLockWithShortTimeoutAsync(SchedulerApiFactory factory, int productId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MasterDataDbContext context = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using OpenTransaction transaction = await scope.ServiceProvider.GetRequiredService<RequestTransaction>().BeginAsync(context, cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '200ms'", cancellationToken);
        return await context.LockRowAsync<Product>(productId, cancellationToken);
    }

    private static async Task<int> CreateProductAsync(SchedulerApiFactory factory, string code)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MasterDataDbContext context = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        Product product = new() { Code = code, Name = $"Product {code}" };
        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }
}

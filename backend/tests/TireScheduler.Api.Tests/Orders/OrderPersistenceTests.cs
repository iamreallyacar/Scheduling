using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.Orders;

public sealed class OrderPersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_17_when_saving_the_lots_fails_nothing_of_the_order_is_saved()
    {
        await using SchedulerApiFactory factory = new(
            postgres.NewDatabaseConnectionString(),
            replaceServices: services => services.ConfigureDbContext<OrdersDbContext>(options => options.AddInterceptors(new FailingLotInsert())));
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC17");

        HttpResponseMessage response = await admin.PostAsync(OrdersPath, NewOrder("SO-17", productId));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        OrdersDbContext context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        Assert.False(await context.Orders.IgnoreQueryFilters().AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await context.Operations.AnyAsync(TestContext.Current.CancellationToken));
    }

    // Fails the command that writes the lots, which runs after the order and its operations.
    private sealed class FailingLotInsert : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("INSERT INTO orders.\"Lots\"", StringComparison.Ordinal)
                ? throw new InvalidOperationException("Simulated failure while saving the lots.")
                : base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

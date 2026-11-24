using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.MasterData.Products;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.MasterData;

// AC-222: a product or machine type delete and a write that refers to the same row lock it
// (DEC-66). Each test holds the row in a transaction that does what the other command would,
// and checks that the write waits and then sees it.
public sealed class ReferenceLockTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    // The delete removes the held row, so it would wait for it even without its lock, but only
    // after its checks. The holder adds an order for the product first, so only a delete that
    // locks the row before it checks sees the order.
    [Fact]
    public async Task AC_222_AC_20_a_product_delete_locks_the_product_before_it_looks_for_orders()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC222-DELETED");

        await using (HeldRowLock heldProduct = await HeldRowLock.TakeAsync<MasterDataDbContext, Product>(api.Factory, productId))
        {
            await heldProduct.ChangeWhileHeldAsync<OrdersDbContext>((orders, _) =>
            {
                orders.Orders.Add(new Order
                {
                    OrderNo = "SO-222-HELD",
                    CustomerName = "Tire Shop North",
                    ProductId = productId,
                    Quantity = 10,
                    DueDate = new DateOnly(2026, 12, 1),
                    CreatedAt = DateTimeOffset.UnixEpoch,
                });
                return Task.CompletedTask;
            });
            Task<HttpResponseMessage> delete = admin.DeleteAsync($"{ProductsPath}/{productId}");
            await heldProduct.ReleaseOnceItWaitsAsync(delete);

            JsonElement problem = await ProblemAssert.HasProblemAsync(await delete, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
            Assert.Contains("SO-222-HELD", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{ProductsPath}/{productId}")).StatusCode);
    }

    // As above, with a machine of the type added while the type is held.
    [Fact]
    public async Task AC_222_AC_20_a_machine_type_delete_locks_the_type_before_it_looks_for_machines()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-222 deleted type");

        await using HeldRowLock heldType = await HeldRowLock.TakeAsync<MasterDataDbContext, ResourceType>(api.Factory, typeId);
        await heldType.ChangeWhileHeldAsync<MasterDataDbContext>(async (masterData, cancellationToken) =>
        {
            int plantDefaultCalendarId = await masterData.Calendars
                .Where(calendar => calendar.IsPlantDefault)
                .Select(calendar => calendar.Id)
                .SingleAsync(cancellationToken);
            masterData.Resources.Add(new Resource { Name = "AC-222 held machine", ResourceTypeId = typeId, CalendarId = plantDefaultCalendarId });
        });
        Task<HttpResponseMessage> delete = admin.DeleteAsync($"{ResourceTypesPath}/{typeId}");
        await heldType.ReleaseOnceItWaitsAsync(delete);

        JsonElement problem = await ProblemAssert.HasProblemAsync(await delete, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Contains("AC-222 held machine", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    // The holder deletes the product, as a product delete under the same lock would. A write that
    // read the routing without the lock would save an order for a product that no longer exists.
    [Theory]
    [InlineData(ProductWrite.CreateOrder)]
    [InlineData(ProductWrite.ChangeOrderProduct)]
    public async Task AC_222_an_order_write_locks_the_product_before_it_reads_the_routing(ProductWrite productWrite)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int deletedProductId = await CreateRoutedProductAsync(admin, $"AC222-{productWrite}");
        int keptProductId = await CreateRoutedProductAsync(admin, $"AC222-{productWrite}-KEPT");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        string orderNo = $"SO-222-{productWrite}";
        int keptOrderId = (await CreateOrderAsync(planner, NewOrder($"{orderNo}-KEPT", keptProductId))).GetProperty("id").GetInt32();

        await using HeldRowLock heldProduct = await HeldRowLock.TakeAsync<MasterDataDbContext, Product>(api.Factory, deletedProductId);
        await heldProduct.ChangeWhileHeldAsync<MasterDataDbContext>((masterData, cancellationToken) =>
            masterData.Products.Where(product => product.Id == deletedProductId).ExecuteDeleteAsync(cancellationToken));
        Task<HttpResponseMessage> write = productWrite switch
        {
            ProductWrite.CreateOrder => planner.PostAsync(OrdersPath, NewOrder(orderNo, deletedProductId)),
            ProductWrite.ChangeOrderProduct => planner.PutAsync(OrderPath(keptOrderId), NewOrder($"{orderNo}-KEPT", deletedProductId)),
            _ => throw new ArgumentOutOfRangeException(nameof(productWrite), productWrite, "No request for this write."),
        };
        await heldProduct.ReleaseOnceItWaitsAsync(write);

        JsonElement problem = await ProblemAssert.HasProblemAsync(await write, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["productId"], ProblemAssert.ErrorFields(problem));
        Assert.Equal(keptProductId, (await planner.GetJsonAsync(OrderPath(keptOrderId))).GetProperty("product").GetProperty("id").GetInt32());
    }

    // The holder deletes the type, as a type delete under the same lock would. Without the lock the
    // write would pass its check and then fail at the foreign key with 500.
    [Theory]
    [InlineData(TypeWrite.SaveRouting, "stages[0].resourceTypeId")]
    [InlineData(TypeWrite.CreateMachine, "resourceTypeId")]
    [InlineData(TypeWrite.ChangeMachineType, "resourceTypeId")]
    public async Task AC_222_a_write_that_names_a_machine_type_locks_the_type_before_it_checks_it(TypeWrite typeWrite, string expectedField)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int deletedTypeId = await CreateResourceTypeAsync(admin, $"AC-222 {typeWrite} deleted");
        int keptTypeId = await CreateResourceTypeAsync(admin, $"AC-222 {typeWrite} kept");
        int productId = await CreateProductAsync(admin, $"AC222-{typeWrite}");
        int machineId = await CreateResourceAsync(admin, $"AC-222 {typeWrite} machine", keptTypeId);

        await using HeldRowLock heldType = await HeldRowLock.TakeAsync<MasterDataDbContext, ResourceType>(api.Factory, deletedTypeId);
        await heldType.ChangeWhileHeldAsync<MasterDataDbContext>((masterData, cancellationToken) =>
            masterData.ResourceTypes.Where(type => type.Id == deletedTypeId).ExecuteDeleteAsync(cancellationToken));
        Task<HttpResponseMessage> write = typeWrite switch
        {
            TypeWrite.SaveRouting => admin.PutAsync(RoutingPath(productId), new { stages = new[] { Stage(1, deletedTypeId) }, dependencies = Array.Empty<object>() }),
            TypeWrite.CreateMachine => admin.PostAsync(ResourcesPath, new { name = $"AC-222 {typeWrite} new machine", resourceTypeId = deletedTypeId }),
            TypeWrite.ChangeMachineType => admin.PutAsync($"{ResourcesPath}/{machineId}", new { name = $"AC-222 {typeWrite} machine", resourceTypeId = deletedTypeId, isActive = true }),
            _ => throw new ArgumentOutOfRangeException(nameof(typeWrite), typeWrite, "No request for this write."),
        };
        await heldType.ReleaseOnceItWaitsAsync(write);

        JsonElement problem = await ProblemAssert.HasProblemAsync(await write, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal([expectedField], ProblemAssert.ErrorFields(problem));
    }

    public enum ProductWrite
    {
        CreateOrder,
        ChangeOrderProduct,
    }

    public enum TypeWrite
    {
        SaveRouting,
        CreateMachine,
        ChangeMachineType,
    }
}

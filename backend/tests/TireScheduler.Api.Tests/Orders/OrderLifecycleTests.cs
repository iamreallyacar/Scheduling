using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.Fakes;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Orders;

// The order rules that depend on placements and progress (A13), with real placements and the
// progress facts given through FakeLotActivity, and the times the order records on the injected clock.
public sealed class OrderLifecycleTests : IAsyncDisposable
{
    private static readonly DateTimeOffset CreationTime = Berlin(2, 8, 30);

    private readonly FakeLotActivity lotActivity = new();
    private readonly FakeTimeProvider clock = new(CreationTime);
    private readonly SchedulerApiFixture api;

    public OrderLifecycleTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock, replaceServices: lotActivity.ReplaceProgressIn);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_45_A19_creation_and_cancellation_times_come_from_the_injected_clock()
    {
        using ApiSession planner = await PlannerWithProductAsync("TIMES");
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-TIMES", await ProductIdAsync(planner, "TIMES")));
        clock.Advance(TimeSpan.FromHours(3));

        JsonElement cancelled = await CancelOrderAsync(planner, order.GetProperty("id").GetInt32());

        Assert.Equal(CreationTime, order.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(CreationTime.AddHours(3), cancelled.GetProperty("cancelledAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData(LotFact.Placed)]
    [InlineData(LotFact.Started)]
    public async Task AC_49_product_or_quantity_of_an_order_with_a_placed_or_started_lot_cannot_change_and_AC_47_other_fields_can(LotFact lotFact)
    {
        using ApiSession planner = await PlannerWithProductAsync("LOCKED");
        int productId = await ProductIdAsync(planner, "LOCKED");
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-LOCKED", productId, quantity: 10));
        int orderId = order.GetProperty("id").GetInt32();
        await RecordAsync(lotFact, order, LotIds(order)[^1]);
        JsonElement recorded = await planner.GetJsonAsync(OrderPath(orderId));

        HttpResponseMessage quantityChange = await planner.PutAsync(OrderPath(orderId), NewOrder("SO-LOCKED", productId, quantity: 11));
        HttpResponseMessage customerChange = await planner.PutAsync(OrderPath(orderId), NewOrder("SO-LOCKED", productId, quantity: 10, customerName: "New customer"));

        await ProblemAssert.HasProblemAsync(quantityChange, HttpStatusCode.Conflict, ProblemCodes.OrderLocked);
        Assert.Equal(HttpStatusCode.NoContent, customerChange.StatusCode);
        JsonElement saved = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal("New customer", saved.GetProperty("customerName").GetString());
        Assert.Equal(recorded.GetProperty("operations").GetRawText(), saved.GetProperty("operations").GetRawText());
    }

    [Fact]
    public async Task AC_51_an_order_with_a_lot_in_progress_cannot_be_cancelled_but_one_with_only_completed_lots_can()
    {
        using ApiSession planner = await PlannerWithProductAsync("RUNNING");
        int productId = await ProductIdAsync(planner, "RUNNING");
        JsonElement running = await CreateOrderAsync(planner, NewOrder("SO-RUNNING", productId));
        JsonElement finished = await CreateOrderAsync(planner, NewOrder("SO-FINISHED", productId));
        lotActivity.StartedLotIds.UnionWith([LotIds(running)[0], .. LotIds(finished)]);
        lotActivity.CompletedLotIds.UnionWith(LotIds(finished));

        HttpResponseMessage cancelRunning = await planner.PostAsync($"{OrderPath(running.GetProperty("id").GetInt32())}/cancel");
        HttpResponseMessage cancelFinished = await planner.PostAsync($"{OrderPath(finished.GetProperty("id").GetInt32())}/cancel");

        await ProblemAssert.HasProblemAsync(cancelRunning, HttpStatusCode.Conflict, ProblemCodes.OrderLotInProgress);
        Assert.Equal(JsonValueKind.Null, (await planner.GetJsonAsync(OrderPath(running.GetProperty("id").GetInt32()))).GetProperty("cancelledAt").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, cancelFinished.StatusCode);
    }

    [Theory]
    [InlineData(LotFact.Placed)]
    [InlineData(LotFact.Started)]
    public async Task AC_19_an_order_with_a_placed_or_started_lot_cannot_be_deleted(LotFact lotFact)
    {
        using ApiSession planner = await PlannerWithProductAsync("KEPT");
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-KEPT", await ProductIdAsync(planner, "KEPT")));
        await RecordAsync(lotFact, order, LotIds(order)[0]);

        HttpResponseMessage response = await planner.DeleteAsync(OrderPath(order.GetProperty("id").GetInt32()));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.OrderNotDeletable);
        Assert.Equal(HttpStatusCode.OK, (await planner.GetAsync(OrderPath(order.GetProperty("id").GetInt32()))).StatusCode);
    }

    // Each of these writes changes the order row, so it would wait for a held row even without its
    // lock, but only after its checks had read the lots. The lot starts while the row is held, as a
    // start under the same lock would (DEC-62), so only a write that locks the row before its
    // checks sees the start and is rejected.
    [Theory]
    [InlineData(OrderWrite.Update, ProblemCodes.OrderLocked)]
    [InlineData(OrderWrite.Cancel, ProblemCodes.OrderLotInProgress)]
    [InlineData(OrderWrite.Delete, ProblemCodes.OrderNotDeletable)]
    public async Task AC_19_AC_49_AC_51_an_order_write_locks_the_order_row_before_it_checks_the_lots(OrderWrite orderWrite, string expectedCode)
    {
        using ApiSession planner = await PlannerWithProductAsync("HELD");
        int productId = await ProductIdAsync(planner, "HELD");
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-HELD", productId, quantity: 10));
        int orderId = order.GetProperty("id").GetInt32();

        await using HeldRowLock heldOrder = await HeldRowLock.TakeAsync<OrdersDbContext, Order>(api.Factory, orderId);
        Task<HttpResponseMessage> write = orderWrite switch
        {
            OrderWrite.Update => planner.PutAsync(OrderPath(orderId), NewOrder("SO-HELD", productId, quantity: 11)),
            OrderWrite.Cancel => planner.PostAsync($"{OrderPath(orderId)}/cancel"),
            OrderWrite.Delete => planner.DeleteAsync(OrderPath(orderId)),
            _ => throw new ArgumentOutOfRangeException(nameof(orderWrite), orderWrite, "No request for this write."),
        };
        await heldOrder.ReleaseOnceItWaitsAsync(write, whileHeld: () => lotActivity.StartedLotIds.Add(LotIds(order)[0]));

        await ProblemAssert.HasProblemAsync(await write, HttpStatusCode.Conflict, expectedCode);
    }

    public enum OrderWrite
    {
        Update,
        Cancel,
        Delete,
    }

    // A cancel deletes the placements of its order and a working time change updates them one at a
    // time under the machine locks. The cancel takes the same machine locks first, so the two run
    // one after the other instead of taking two placement rows in opposite orders (DEC-67).
    [Fact]
    public async Task AC_223_AC_50_a_cancel_waits_while_another_command_holds_a_machine()
    {
        using ApiSession planner = await PlannerWithProductAsync("CANCELHELD");
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-CANCELHELD", await ProductIdAsync(planner, "CANCELHELD")));
        int orderId = order.GetProperty("id").GetInt32();
        int lotId = LotIds(order)[0];
        await RecordAsync(LotFact.Placed, order, lotId);
        int resourceId = Lot(await planner.GetJsonAsync(OrderPath(orderId)), lotId).GetProperty("placement").GetProperty("resource").GetProperty("id").GetInt32();

        await using HeldRowLock heldMachine = await HeldRowLock.TakeAsync<MasterDataDbContext, Resource>(api.Factory, resourceId);
        Task<HttpResponseMessage> cancel = planner.PostAsync($"{OrderPath(orderId)}/cancel");
        await heldMachine.ReleaseOnceItWaitsAsync(cancel);

        Assert.Equal(HttpStatusCode.NoContent, (await cancel).StatusCode);
        Assert.Equal(JsonValueKind.Null, Lot(await planner.GetJsonAsync(OrderPath(orderId)), lotId).GetProperty("placement").ValueKind);
    }

    public enum LotFact
    {
        Placed,
        Started,
    }

    // A placed lot is placed in the live version on a new machine of its type.
    private async Task RecordAsync(LotFact lotFact, JsonElement order, int lotId)
    {
        if (lotFact == LotFact.Started)
        {
            lotActivity.StartedLotIds.Add(lotId);
            return;
        }

        int resourceTypeId = order.GetProperty("operations").EnumerateArray()
            .Single(operation => operation.GetProperty("lots").EnumerateArray().Any(lot => lot.GetProperty("id").GetInt32() == lotId))
            .GetProperty("resourceType").GetProperty("id").GetInt32();
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateResourceAsync(admin, $"Machine for lot {lotId}", resourceTypeId);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        await PlaceLotAsync(planner, lotId, resourceId, Berlin(16, 8));
    }

    private async Task<ApiSession> PlannerWithProductAsync(string productCode)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        await CreateRoutedProductAsync(admin, productCode);
        return await api.Factory.SignInAsRoleAsync(UserRole.Planner);
    }

    private static async Task<int> ProductIdAsync(ApiSession session, string productCode) =>
        (await session.GetJsonAsync("/api/v1/products")).EnumerateArray()
            .Single(product => product.GetProperty("code").GetString() == productCode)
            .GetProperty("id").GetInt32();
}

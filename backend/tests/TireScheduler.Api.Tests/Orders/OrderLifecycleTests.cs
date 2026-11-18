using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.Orders;

// The order rules that depend on placements and progress (A13), with those facts given through
// the Orders ports, and the times the order records on the injected clock.
public sealed class OrderLifecycleTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreationTime = new(2026, 11, 2, 7, 30, 0, TimeSpan.Zero);

    private readonly FakeLotActivity lotActivity = new();
    private readonly FakeTimeProvider clock = new(CreationTime);
    private SchedulerApiFactory factory = null!;

    public ValueTask InitializeAsync()
    {
        factory = new SchedulerApiFactory(postgres.NewDatabaseConnectionString(), clock: clock, replaceServices: services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ILotPlacements>(lotActivity));
            services.Replace(ServiceDescriptor.Singleton<ILotProgress>(lotActivity));
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();

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
        Record(lotFact, LotIds(order)[^1]);

        HttpResponseMessage quantityChange = await planner.PutAsync(OrderPath(orderId), NewOrder("SO-LOCKED", productId, quantity: 11));
        HttpResponseMessage customerChange = await planner.PutAsync(OrderPath(orderId), NewOrder("SO-LOCKED", productId, quantity: 10, customerName: "New customer"));

        await ProblemAssert.HasProblemAsync(quantityChange, HttpStatusCode.Conflict, ProblemCodes.OrderLocked);
        Assert.Equal(HttpStatusCode.NoContent, customerChange.StatusCode);
        JsonElement saved = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal("New customer", saved.GetProperty("customerName").GetString());
        Assert.Equal(order.GetProperty("operations").GetRawText(), saved.GetProperty("operations").GetRawText());
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
        Record(lotFact, LotIds(order)[0]);

        HttpResponseMessage response = await planner.DeleteAsync(OrderPath(order.GetProperty("id").GetInt32()));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.OrderNotDeletable);
        Assert.Equal(HttpStatusCode.OK, (await planner.GetAsync(OrderPath(order.GetProperty("id").GetInt32()))).StatusCode);
    }

    public enum LotFact
    {
        Placed,
        Started,
    }

    private void Record(LotFact lotFact, int lotId) =>
        (lotFact == LotFact.Placed ? lotActivity.PlacedLotIds : lotActivity.StartedLotIds).Add(lotId);

    private async Task<ApiSession> PlannerWithProductAsync(string productCode)
    {
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        await CreateRoutedProductAsync(admin, productCode);
        return await factory.SignInAsRoleAsync(UserRole.Planner);
    }

    private static async Task<int> ProductIdAsync(ApiSession session, string productCode) =>
        (await session.GetJsonAsync("/api/v1/products")).EnumerateArray()
            .Single(product => product.GetProperty("code").GetString() == productCode)
            .GetProperty("id").GetInt32();
}

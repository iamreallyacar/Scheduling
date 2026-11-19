using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;

namespace TireScheduler.Api.Tests.Queries;

// F-ORD-2 through the order reads of Queries (DEC-29), on a fixed clock in the plant time zone
// Europe/Berlin, for orders without placements; Scheduled and the late flag from a planned end are
// in Scheduling/PlacementTests.cs. Until task M5-1 stores progress, no lot is started.
public sealed class OrderStatusTests : IAsyncDisposable
{
    private const string DueDate = "2026-11-10";

    private static readonly DateTimeOffset LastMinuteOfDueDay = Berlin(10, 23, 59);

    // A session ends after 12 hours without a request (A21), so the clock starts close to the due day.
    private readonly FakeTimeProvider clock = new(LastMinuteOfDueDay.AddHours(-2));
    private readonly SchedulerApiFixture api;

    public OrderStatusTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: clock);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_53_a_new_order_is_pending_and_not_late_in_the_detail_and_the_list()
    {
        using ApiSession admin = await AdminAsync();
        int orderId = (await CreateOrderAsync(admin, NewOrder("SO-53", await CreateRoutedProductAsync(admin, "AC53"), dueDate: DueDate))).GetProperty("id").GetInt32();

        JsonElement order = await admin.GetJsonAsync(OrderPath(orderId));
        JsonElement listed = ListedOrder(await admin.GetJsonAsync(OrdersPath), orderId);

        Assert.Equal(("Pending", false), StatusAndLate(order));
        Assert.Equal(("Pending", false), StatusAndLate(listed));
    }

    [Fact]
    public async Task AC_54_a_cancelled_order_is_cancelled_and_never_late_whatever_the_dates()
    {
        using ApiSession admin = await AdminAsync();
        int orderId = (await CreateOrderAsync(admin, NewOrder("SO-54", await CreateRoutedProductAsync(admin, "AC54"), dueDate: DueDate))).GetProperty("id").GetInt32();
        await CancelOrderAsync(admin, orderId);
        clock.SetUtcNow(LastMinuteOfDueDay.AddDays(30));
        using ApiSession later = await AdminAsync();

        JsonElement order = await later.GetJsonAsync(OrderPath(orderId));
        JsonElement listed = ListedOrder(await later.GetJsonAsync(OrdersPath), orderId);

        Assert.Equal(("Cancelled", false), StatusAndLate(order));
        Assert.Equal(("Cancelled", false), StatusAndLate(listed));
    }

    [Fact]
    public async Task AC_55_AC_56_an_unfinished_order_turns_late_at_midnight_plant_time_after_its_due_day()
    {
        using ApiSession admin = await AdminAsync();
        int orderId = (await CreateOrderAsync(admin, NewOrder("SO-56", await CreateRoutedProductAsync(admin, "AC56"), dueDate: DueDate))).GetProperty("id").GetInt32();

        clock.SetUtcNow(LastMinuteOfDueDay);
        JsonElement beforeMidnight = await admin.GetJsonAsync(OrderPath(orderId));
        clock.Advance(TimeSpan.FromMinutes(1));
        JsonElement atMidnight = await admin.GetJsonAsync(OrderPath(orderId));
        JsonElement listedAtMidnight = ListedOrder(await admin.GetJsonAsync(OrdersPath), orderId);

        Assert.Equal(("Pending", false), StatusAndLate(beforeMidnight));
        Assert.Equal(("Pending", true), StatusAndLate(atMidnight));
        Assert.Equal(("Pending", true), StatusAndLate(listedAtMidnight));
    }

    private async Task<ApiSession> AdminAsync() => await api.Factory.SignInAsRoleAsync(UserRole.Admin);

    private static (string?, bool) StatusAndLate(JsonElement order) =>
        (order.GetProperty("status").GetString(), order.GetProperty("isLate").GetBoolean());
}

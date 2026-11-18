using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.MasterData;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.Orders;

// F-ORD-5: the order detail. Until tasks M3-4 and M5-1 store placements and progress, a lot is
// Unscheduled or Cancelled and no quantity is completed.
public sealed class OrderDetailTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 7, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AC_70_the_detail_shows_the_order_its_progress_and_its_operations_in_sequence_with_dependencies_and_lot_status()
    {
        FakeTimeProvider clock = new(Now);
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), clock: clock);
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int buildingTypeId = await CreateResourceTypeAsync(admin, "AC-70 building");
        int curingTypeId = await CreateResourceTypeAsync(admin, "AC-70 curing");
        int productId = await CreateProductAsync(admin, "AC70");
        await SaveRoutingAsync(
            admin,
            productId,
            [Stage(2, curingTypeId, cycleMinutes: 12, setupMinutes: 30, unitsPerCycle: 2), Stage(1, buildingTypeId, cycleMinutes: 5)],
            [Dependency(1, 2, minLagMinutes: 120)]);
        using ApiSession planner = await factory.SignInAsRoleAsync(UserRole.Planner);
        int orderId = (await CreateOrderAsync(planner, NewOrder("SO-70", productId, quantity: 11, dueDate: "2026-11-01", priority: "High"))).GetProperty("id").GetInt32();
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement detail = await viewer.GetJsonAsync(OrderPath(orderId));

        Assert.Equal(
            ("SO-70", "Tire Shop North", "AC70", 11, "2026-11-01", "High", "Pending", true),
            (detail.GetProperty("orderNo").GetString(),
                detail.GetProperty("customerName").GetString(),
                detail.GetProperty("product").GetProperty("code").GetString(),
                detail.GetProperty("quantity").GetInt32(),
                detail.GetProperty("dueDate").GetString(),
                detail.GetProperty("priority").GetString(),
                detail.GetProperty("status").GetString(),
                detail.GetProperty("isLate").GetBoolean()));
        Assert.Equal(Now, detail.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("cancelledAt").ValueKind);
        Assert.Equal((0, 22), ProgressValues(detail));
        JsonElement[] operations = detail.GetProperty("operations").EnumerateArray().ToArray();
        Assert.Equal(
            [(1, "AC-70 building", 11, 0, 5, 1), (2, "AC-70 curing", 11, 30, 12, 2)],
            operations.Select(operation => (
                operation.GetProperty("sequenceNo").GetInt32(),
                operation.GetProperty("resourceType").GetProperty("name").GetString(),
                operation.GetProperty("quantity").GetInt32(),
                operation.GetProperty("setupMinutes").GetInt32(),
                operation.GetProperty("cycleMinutes").GetInt32(),
                operation.GetProperty("unitsPerCycle").GetInt32())));
        Assert.Equal([(1, 2, 120)], detail.GetProperty("dependencies").EnumerateArray().Select(DependencyValues));
        // 11 x 5 = 55; 30 + ceil(11 / 2) x 12 = 102.
        Assert.Equal([[(1, 11, 55, "Unscheduled")], [(1, 11, 102, "Unscheduled")]], operations.Select(LotValues));
    }

    [Fact]
    public async Task AC_70_A19_a_cancelled_order_shows_its_cancellation_time_and_its_lots_as_cancelled()
    {
        FakeTimeProvider clock = new(Now);
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), clock: clock);
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC70C");
        int orderId = (await CreateOrderAsync(admin, NewOrder("SO-70C", productId, quantity: 4))).GetProperty("id").GetInt32();
        clock.Advance(TimeSpan.FromHours(2));

        JsonElement detail = await CancelOrderAsync(admin, orderId);

        Assert.Equal(("Cancelled", false), (detail.GetProperty("status").GetString(), detail.GetProperty("isLate").GetBoolean()));
        Assert.Equal(Now.AddHours(2), detail.GetProperty("cancelledAt").GetDateTimeOffset());
        Assert.Equal((0, 8), ProgressValues(detail));
        Assert.All(
            detail.GetProperty("operations").EnumerateArray().SelectMany(operation => operation.GetProperty("lots").EnumerateArray()),
            lot => Assert.Equal("Cancelled", lot.GetProperty("status").GetString()));
    }

    private static (int CompletedQuantity, int TotalQuantity) ProgressValues(JsonElement order)
    {
        JsonElement progress = order.GetProperty("progress");
        return (progress.GetProperty("completedQuantity").GetInt32(), progress.GetProperty("totalQuantity").GetInt32());
    }

    private static (int Predecessor, int Successor, int MinLag) DependencyValues(JsonElement dependency) =>
        (dependency.GetProperty("predecessorSequenceNo").GetInt32(),
            dependency.GetProperty("successorSequenceNo").GetInt32(),
            dependency.GetProperty("minLagMinutes").GetInt32());

    private static (int LotNo, int Quantity, int DurationMinutes, string? Status)[] LotValues(JsonElement operation) =>
        operation.GetProperty("lots").EnumerateArray()
            .Select(lot => (lot.GetProperty("lotNo").GetInt32(), lot.GetProperty("quantity").GetInt32(), lot.GetProperty("durationMinutes").GetInt32(), lot.GetProperty("status").GetString()))
            .ToArray();
}

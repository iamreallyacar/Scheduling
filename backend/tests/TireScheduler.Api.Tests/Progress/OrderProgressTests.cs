using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.ProgressTestData;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Progress;

// F-PRG-2 (acceptance-criteria.md section 9): order progress derived from the completed lots, with
// real progress and placements on the plant default calendar in Europe/Berlin.
public sealed class OrderProgressTests : IAsyncDisposable
{
    // Friday afternoon of the week the lots are placed in, so their actual times can lie before now.
    private static readonly DateTimeOffset Now = Berlin(20, 12);

    private readonly SchedulerApiFixture api;

    public OrderProgressTests(PostgresFixture postgres) => api = SchedulerApiFixture.ForEachTest(postgres, clock: new FakeTimeProvider(Now));

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AC_215_progress_sums_the_completed_lots_of_every_stage_and_rounds_the_percentage_down()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC215");
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        // A third stage on the building machines, so a third of the work is not a whole percentage.
        int productId = await CreateProductAsync(admin, "AC215-3");
        await SaveRoutingAsync(
            admin,
            productId,
            [Stage(1, plant.BuildingTypeId, cycleMinutes: 12), Stage(2, plant.CuringTypeId, cycleMinutes: 24), Stage(3, plant.BuildingTypeId, cycleMinutes: 12)],
            [Dependency(1, 2), Dependency(2, 3)]);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-215", productId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        int[] lots = LotIds(order);
        await PlaceLotAsync(planner, lots[0], plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, lots[1], plant.Press1, Berlin(16, 10));
        await PlaceLotAsync(planner, lots[2], plant.Builder1, Berlin(16, 14));

        JsonElement notStarted = await planner.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, lots[0], Berlin(16, 8), Berlin(16, 10));
        await RecordProgressAsync(planner, lots[1], Berlin(16, 10));
        JsonElement oneCompleted = await planner.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, lots[1], Berlin(16, 10), Berlin(16, 14));
        await RecordProgressAsync(planner, lots[2], Berlin(16, 14));
        JsonElement twoCompleted = await planner.GetJsonAsync(OrderPath(orderId));
        await RecordProgressAsync(planner, lots[2], Berlin(16, 14), Berlin(16, 16));
        JsonElement allCompleted = await planner.GetJsonAsync(OrderPath(orderId));

        Assert.Equal(
            [(0, 30, 0), (10, 30, 33), (20, 30, 66), (30, 30, 100)],
            [ProgressValues(notStarted), ProgressValues(oneCompleted), ProgressValues(twoCompleted), ProgressValues(allCompleted)]);
    }

    [Fact]
    public async Task AC_216_a_cancelled_order_keeps_the_progress_of_its_completed_lots()
    {
        TestPlant plant = await CreatePlantAsync(api.Factory, "AC216");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-216", plant.ProductId, quantity: LotQuantity));
        int orderId = order.GetProperty("id").GetInt32();
        (int buildingLot, int curingLot) = (LotIds(order)[0], LotIds(order)[1]);
        await PlaceLotAsync(planner, buildingLot, plant.Builder1, Berlin(16, 8));
        await PlaceLotAsync(planner, curingLot, plant.Press1, Berlin(16, 10));
        await RecordProgressAsync(planner, buildingLot, Berlin(16, 8), Berlin(16, 10));

        JsonElement cancelled = await CancelOrderAsync(planner, orderId);

        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal((10, 20, 50), ProgressValues(await planner.GetJsonAsync(OrderPath(orderId))));
    }

    private static (int CompletedQuantity, int TotalQuantity, int PercentComplete) ProgressValues(JsonElement order)
    {
        JsonElement progress = order.GetProperty("progress");
        return (progress.GetProperty("completedQuantity").GetInt32(), progress.GetProperty("totalQuantity").GetInt32(), progress.GetProperty("percentComplete").GetInt32());
    }
}

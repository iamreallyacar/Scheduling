using System.Net;
using System.Text.Json;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Queries;

// F-SCH-7: the lots that wait for placement, with a lot placed through the placement endpoint.
public sealed class UnscheduledLotQueueTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_174_lists_the_unscheduled_lots_of_open_orders_by_due_date_order_number_and_stage()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int buildingTypeId = await CreateResourceTypeAsync(admin, "AC-174 building");
        int curingTypeId = await CreateResourceTypeAsync(admin, "AC-174 curing");
        int productId = await CreateProductAsync(admin, "AC174");
        await SaveRoutingAsync(
            admin,
            productId,
            [Stage(1, buildingTypeId, cycleMinutes: 5), Stage(2, curingTypeId, cycleMinutes: 12, setupMinutes: 30, unitsPerCycle: 2)],
            [Dependency(1, 2, minLagMinutes: 120)]);
        using ApiSession planner = await factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement laterNumber = await CreateOrderAsync(planner, NewOrder("SO-B", productId, quantity: 10, dueDate: "2026-11-20"));
        JsonElement earlierNumber = await CreateOrderAsync(planner, NewOrder("SO-A", productId, quantity: 4, dueDate: "2026-11-20"));
        JsonElement partlyPlaced = await CreateOrderAsync(planner, NewOrder("SO-C", productId, quantity: 6, dueDate: "2026-11-18"));
        JsonElement cancelled = await CreateOrderAsync(planner, NewOrder("SO-X", productId, dueDate: "2026-11-10"));
        await CancelOrderAsync(planner, cancelled.GetProperty("id").GetInt32());
        JsonElement deleted = await CreateOrderAsync(planner, NewOrder("SO-D", productId, dueDate: "2026-11-12"));
        HttpResponseMessage deleteResponse = await planner.DeleteAsync(OrderPath(deleted.GetProperty("id").GetInt32()));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        // Mon 2026-11-16 08:00 in Berlin.
        await PlaceLotAsync(planner, LotIds(partlyPlaced)[0], await CreateResourceAsync(admin, "AC-174 builder", buildingTypeId), new DateTimeOffset(2026, 11, 16, 7, 0, 0, TimeSpan.Zero));
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement queue = await viewer.GetJsonAsync(UnscheduledLotsPath("live"));

        JsonElement[] lots = queue.EnumerateArray().ToArray();
        Assert.Equal(
            [("SO-C", 2), ("SO-A", 1), ("SO-A", 2), ("SO-B", 1), ("SO-B", 2)],
            lots.Select(lot => (lot.GetProperty("orderNo").GetString(), lot.GetProperty("sequenceNo").GetInt32())));
        JsonElement curingLot = lots[2];
        // 30 + ceil(4 / 2) x 12 = 54.
        Assert.Equal(
            (LotIds(earlierNumber)[1], 1, 4, 54, earlierNumber.GetProperty("id").GetInt32(), "2026-11-20", curingTypeId, "AC-174 curing"),
            (curingLot.GetProperty("id").GetInt32(),
                curingLot.GetProperty("lotNo").GetInt32(),
                curingLot.GetProperty("quantity").GetInt32(),
                curingLot.GetProperty("durationMinutes").GetInt32(),
                curingLot.GetProperty("orderId").GetInt32(),
                curingLot.GetProperty("dueDate").GetString(),
                curingLot.GetProperty("resourceType").GetProperty("id").GetInt32(),
                curingLot.GetProperty("resourceType").GetProperty("name").GetString()));
        Assert.Equal([(1, buildingTypeId, "AC-174 building", 120)], PredecessorValues(curingLot));
        Assert.Empty(PredecessorValues(lots[1]));
        Assert.Equal(LotIds(laterNumber)[0], lots[3].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task AC_174_AC_23_a_version_that_does_not_exist_is_not_found()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage response = await viewer.GetAsync(UnscheduledLotsPath("999"));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.Empty((await viewer.GetJsonAsync(UnscheduledLotsPath("live"))).EnumerateArray());
    }

    private static (int SequenceNo, int ResourceTypeId, string? ResourceTypeName, int MinLagMinutes)[] PredecessorValues(JsonElement lot) =>
        lot.GetProperty("predecessors").EnumerateArray()
            .Select(predecessor => (
                predecessor.GetProperty("sequenceNo").GetInt32(),
                predecessor.GetProperty("resourceType").GetProperty("id").GetInt32(),
                predecessor.GetProperty("resourceType").GetProperty("name").GetString(),
                predecessor.GetProperty("minLagMinutes").GetInt32()))
            .ToArray();
}

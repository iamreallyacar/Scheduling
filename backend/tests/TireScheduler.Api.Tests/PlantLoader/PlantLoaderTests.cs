using System.Globalization;
using System.Text.Json;
using TireScheduler.Api.Security;
using TireScheduler.Engine;
using TireScheduler.PlantGenerator;
using TireScheduler.PlantLoader;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.PlantLoader;

// The loader against a running API on an empty database of its own (DEC-32).
public sealed class PlantLoaderTests(PostgresFixture postgres)
{
    private static readonly DateOnly StartMonday = new(2026, 11, 16);

    private static readonly DateTimeOffset HorizonStart = PlantTime.StartOfDay(StartMonday, SchedulerApiFactory.DefaultPlantTimeZone);

    [Theory]
    [InlineData("small")]
    [InlineData("plant")]
    [InlineData("stress")]
    public async Task AC_157_every_generated_row_is_created_through_the_api_and_reads_back_with_its_values(string presetName)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        LoaderSettings settings = AdminSettings(presetName, PlantPresets.ByName[presetName], seed: 3);
        GeneratedPlant plant = SyntheticPlant.Generate(settings.Parameters, settings.Seed, settings.StartDate);

        (int exitCode, string output, string errors) = await RunAsync(factory, settings);

        Assert.True(exitCode == 0, errors);
        Assert.Contains("seed 3", output, StringComparison.Ordinal);
        Assert.Contains("start 2026-11-16", output, StringComparison.Ordinal);
        Assert.Contains($"{plant.Orders.Count} orders", output, StringComparison.Ordinal);
        Assert.Contains($"{plant.Unavailabilities.Count} unavailabilities", output, StringComparison.Ordinal);
        Assert.DoesNotContain("not loaded", output, StringComparison.Ordinal);
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);
        await AssertMachinesAsync(viewer, plant);
        await AssertCalendarAsync(viewer, plant);
        await AssertProductsAsync(viewer, plant);
        await AssertOrdersAsync(viewer, plant);
        await AssertUnavailabilitiesAsync(viewer, plant);
    }

    [Theory]
    [InlineData("resource type")]
    [InlineData("product")]
    [InlineData("order")]
    public async Task AC_158_a_plant_that_is_not_empty_is_left_unchanged(string existingKind)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        switch (existingKind)
        {
            case "resource type":
                await admin.PostAsync("/api/v1/resource-types", new { name = "Existing type" });
                break;
            case "product":
                await CreateProductAsync(admin, "EXISTING");
                break;
            default:
                await CreateOrderAsync(admin, NewOrder("SO-EXISTING", await CreateRoutedProductAsync(admin, "EXISTING")));
                break;
        }

        JsonElement resourceTypesBefore = await admin.GetJsonAsync("/api/v1/resource-types");
        JsonElement productsBefore = await admin.GetJsonAsync("/api/v1/products");

        (int exitCode, _, string errors) = await RunAsync(factory, AdminSettings("small", PlantPresets.Small, seed: 1));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("not empty", errors, StringComparison.Ordinal);
        Assert.Contains(existingKind, errors, StringComparison.Ordinal);
        Assert.Equal(resourceTypesBefore.ToString(), (await admin.GetJsonAsync("/api/v1/resource-types")).ToString());
        Assert.Equal(productsBefore.ToString(), (await admin.GetJsonAsync("/api/v1/products")).ToString());
        Assert.Equal(0, (await admin.GetJsonAsync("/api/v1/resources")).GetArrayLength());
        Assert.Equal(0, (await admin.GetJsonAsync("/api/v1/holidays")).GetArrayLength());
    }

    [Fact]
    public async Task AC_159_a_value_the_api_rejects_stops_the_loader_with_the_request_and_the_problem()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        // A machine name longer than the 100 characters of A23.
        PlantParameters tooLongMachineNames = PlantPresets.Small with
        {
            MachineTypes = [.. PlantPresets.Small.MachineTypes.Select(type => type with { MachineNamePrefix = new string('M', 100) })],
        };

        (int exitCode, _, string errors) = await RunAsync(factory, AdminSettings("small", tooLongMachineNames, seed: 1));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("POST /api/v1/resources", errors, StringComparison.Ordinal);
        Assert.Contains("400", errors, StringComparison.Ordinal);
        Assert.Contains("validation.failed", errors, StringComparison.Ordinal);
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        Assert.Equal(PlantPresets.Small.MachineTypes.Count, (await admin.GetJsonAsync("/api/v1/resource-types")).GetArrayLength());
        Assert.Equal(0, (await admin.GetJsonAsync("/api/v1/resources")).GetArrayLength());
    }

    [Fact]
    public async Task AC_159_a_user_who_may_not_write_master_data_is_stopped_at_the_first_write()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        string plannerEmail = "loader-planner@plant.test";
        await factory.EnsureUserAsync(plannerEmail, UserRole.Planner);
        LoaderSettings settings = AdminSettings("small", PlantPresets.Small, seed: 1) with { Email = plannerEmail, Password = SchedulerApiFactory.UserPassword };

        (int exitCode, _, string errors) = await RunAsync(factory, settings);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("POST /api/v1/resource-types", errors, StringComparison.Ordinal);
        Assert.Contains("403", errors, StringComparison.Ordinal);
        Assert.Contains("auth.forbidden", errors, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("small")]
    [InlineData("stress")]
    [InlineData("plant")]
    public async Task AC_203_with_the_schedule_option_every_order_is_placed_from_the_start_of_the_horizon_without_conflicts(string presetName)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        LoaderSettings settings = AdminSettings(presetName, PlantPresets.ByName[presetName], seed: 3) with { PlaceSchedule = true };
        GeneratedPlant plant = SyntheticPlant.Generate(settings.Parameters, settings.Seed, settings.StartDate);

        (int exitCode, string output, string errors) = await RunAsync(factory, settings);

        Assert.True(exitCode == 0, errors);
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);
        JsonElement[] lots = [.. (await ReadOrderDetailsAsync(viewer, plant.Orders.Count)).SelectMany(LotsOf)];
        Assert.All(lots, lot =>
        {
            JsonElement placement = lot.GetProperty("placement");
            Assert.Equal(JsonValueKind.Object, placement.ValueKind);
            Assert.True(placement.GetProperty("plannedStart").GetDateTimeOffset() >= HorizonStart);
        });
        Assert.Contains($"Placed {lots.Length} lots of {plant.Orders.Count} orders", output, StringComparison.Ordinal);
        Assert.Equal(0, (await viewer.GetJsonAsync(ConflictsPath(LiveVersion))).GetArrayLength());
    }

    [Fact]
    public async Task AC_203_without_the_schedule_option_nothing_is_placed()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        LoaderSettings settings = AdminSettings("small", PlantPresets.Small, seed: 3);
        GeneratedPlant plant = SyntheticPlant.Generate(settings.Parameters, settings.Seed, settings.StartDate);

        (int exitCode, string output, string errors) = await RunAsync(factory, settings);

        Assert.True(exitCode == 0, errors);
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);
        Assert.All((await ReadOrderDetailsAsync(viewer, plant.Orders.Count)).SelectMany(LotsOf), lot =>
            Assert.Equal(JsonValueKind.Null, lot.GetProperty("placement").ValueKind));
        Assert.DoesNotContain("Placed", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC_204_an_order_without_a_free_slot_stays_unplaced_and_is_named_while_the_others_are_placed()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        // Long lots and a breakdown with no known end on every machine, so the orders that come
        // last no longer fit before the breakdowns (DEC-61).
        PlantParameters blockedByBreakdowns = PlantPresets.Small with
        {
            Quantity = new(400, 500),
            Breakdowns = PlantPresets.Small.Breakdowns with { ChancePerMachineWeek = 1 },
            OpenEndedBreakdownShare = 1,
        };
        LoaderSettings settings = AdminSettings("small", blockedByBreakdowns, seed: 3) with { PlaceSchedule = true };
        GeneratedPlant plant = SyntheticPlant.Generate(settings.Parameters, settings.Seed, settings.StartDate);

        (int exitCode, string output, string errors) = await RunAsync(factory, settings);

        Assert.True(exitCode == 0, errors);
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);
        JsonElement[] orders = await ReadOrderDetailsAsync(viewer, plant.Orders.Count);
        string[] unplacedOrderNos = [.. orders
            .Where(order => LotsOf(order).All(lot => lot.GetProperty("placement").ValueKind == JsonValueKind.Null))
            .Select(order => order.GetProperty("orderNo").GetString()!)];
        Assert.NotEmpty(unplacedOrderNos);
        Assert.True(unplacedOrderNos.Length < orders.Length, "No order was placed, so the loader's going on was not checked.");
        Assert.All(orders.Where(order => !unplacedOrderNos.Contains(order.GetProperty("orderNo").GetString())), order =>
            Assert.All(LotsOf(order), lot => Assert.Equal(JsonValueKind.Object, lot.GetProperty("placement").ValueKind)));
        Assert.Contains($"{unplacedOrderNos.Length} orders have no free slot within the search horizon: {string.Join(", ", unplacedOrderNos.Order(StringComparer.Ordinal))}", output, StringComparison.Ordinal);
    }

    private static LoaderSettings AdminSettings(string presetName, PlantParameters parameters, int seed) =>
        new(presetName, parameters, seed, StartMonday, new Uri("https://localhost"), SchedulerApiFactory.AdminEmail, SchedulerApiFactory.AdminPassword, PlaceSchedule: false);

    // The details of every order, which list the operations and lots with their placements.
    private static async Task<JsonElement[]> ReadOrderDetailsAsync(ApiSession viewer, int orderCount)
    {
        List<JsonElement> listed = [];
        for (int page = 1; listed.Count < orderCount; page++)
        {
            JsonElement orderPage = await viewer.GetJsonAsync($"{OrdersPath}?pageSize=200&page={page}");
            Assert.Equal(orderCount, orderPage.GetProperty("totalCount").GetInt32());
            listed.AddRange(orderPage.GetProperty("items").EnumerateArray());
        }

        List<JsonElement> details = [];
        foreach (JsonElement order in listed)
        {
            details.Add(await viewer.GetJsonAsync(OrderPath(order.GetProperty("id").GetInt32())));
        }

        return [.. details];
    }

    private static IEnumerable<JsonElement> LotsOf(JsonElement orderDetail) =>
        orderDetail.GetProperty("operations").EnumerateArray().SelectMany(operation => operation.GetProperty("lots").EnumerateArray());

    private static async Task<(int ExitCode, string Output, string Errors)> RunAsync(SchedulerApiFactory factory, LoaderSettings settings)
    {
        using HttpClient client = factory.NewCookieKeepingClient();
        await using StringWriter output = new(CultureInfo.InvariantCulture);
        await using StringWriter errors = new(CultureInfo.InvariantCulture);
        int exitCode = await LoaderRun.RunAsync(settings, client, output, errors, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), errors.ToString());
    }

    private static async Task AssertMachinesAsync(ApiSession viewer, GeneratedPlant plant)
    {
        JsonElement resourceTypes = await viewer.GetJsonAsync("/api/v1/resource-types");
        Assert.Equal(
            plant.MachineTypes.Select(type => (type.Name, "Machine")).Order(),
            resourceTypes.EnumerateArray().Select(type => (type.GetProperty("name").GetString()!, type.GetProperty("kind").GetString()!)).Order());

        JsonElement resources = await viewer.GetJsonAsync("/api/v1/resources");
        Assert.Equal(
            plant.Machines.Select(machine => (machine.Name, machine.MachineTypeName, true)).Order(),
            resources.EnumerateArray()
                .Select(resource => (resource.GetProperty("name").GetString()!, resource.GetProperty("resourceType").GetProperty("name").GetString()!, resource.GetProperty("isActive").GetBoolean()))
                .Order());
    }

    private static async Task AssertCalendarAsync(ApiSession viewer, GeneratedPlant plant)
    {
        JsonElement calendar = (await viewer.GetJsonAsync("/api/v1/calendars")).EnumerateArray().Single(entry => entry.GetProperty("isPlantDefault").GetBoolean());
        Assert.Equal(
            plant.Shifts.Select(shift => (shift.Weekday.ToString(), ShiftTime(shift.StartMinute), ShiftTime(shift.EndMinute))),
            calendar.GetProperty("shifts").EnumerateArray()
                .Select(shift => (shift.GetProperty("weekday").GetString()!, shift.GetProperty("start").GetString()!, shift.GetProperty("end").GetString()!)));

        JsonElement holidays = await viewer.GetJsonAsync("/api/v1/holidays");
        Assert.Equal(
            plant.Holidays.Select(holiday => (holiday.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), holiday.Name)).Order(),
            holidays.EnumerateArray().Select(holiday => (holiday.GetProperty("date").GetString()!, holiday.GetProperty("name").GetString()!)).Order());
    }

    private static async Task AssertProductsAsync(ApiSession viewer, GeneratedPlant plant)
    {
        JsonElement products = await viewer.GetJsonAsync("/api/v1/products");
        Dictionary<string, JsonElement> productByCode = products.EnumerateArray().ToDictionary(product => product.GetProperty("code").GetString()!);
        Assert.Equal(plant.Products.Select(product => product.Code).Order(), productByCode.Keys.Order());
        foreach (GeneratedProduct product in plant.Products)
        {
            JsonElement saved = productByCode[product.Code];
            Assert.Equal((product.Name, product.Spec), (saved.GetProperty("name").GetString()!, saved.GetProperty("spec").GetString()!));
            JsonElement routing = await viewer.GetJsonAsync(RoutingPath(saved.GetProperty("id").GetInt32()));
            Assert.Equal(
                product.Stages.Select(stage => (stage.SequenceNo, stage.MachineTypeName, stage.SetupMinutes, stage.CycleMinutes, stage.UnitsPerCycle)),
                routing.GetProperty("stages").EnumerateArray().Select(stage => (
                    stage.GetProperty("sequenceNo").GetInt32(),
                    stage.GetProperty("resourceType").GetProperty("name").GetString()!,
                    stage.GetProperty("setupMinutes").GetInt32(),
                    stage.GetProperty("cycleMinutes").GetInt32(),
                    stage.GetProperty("unitsPerCycle").GetInt32())));
            Assert.Equal(
                product.Dependencies.Select(dependency => (dependency.PredecessorSequenceNo, dependency.SuccessorSequenceNo, dependency.MinLagMinutes)),
                routing.GetProperty("dependencies").EnumerateArray().Select(dependency => (
                    dependency.GetProperty("predecessorSequenceNo").GetInt32(),
                    dependency.GetProperty("successorSequenceNo").GetInt32(),
                    dependency.GetProperty("minLagMinutes").GetInt32())));
        }
    }

    private static async Task AssertOrdersAsync(ApiSession viewer, GeneratedPlant plant)
    {
        Dictionary<string, GeneratedProduct> productByCode = plant.Products.ToDictionary(product => product.Code);
        List<JsonElement> listed = [];
        for (int page = 1; listed.Count < plant.Orders.Count; page++)
        {
            JsonElement orderPage = await viewer.GetJsonAsync($"{OrdersPath}?pageSize=200&page={page}");
            Assert.Equal(plant.Orders.Count, orderPage.GetProperty("totalCount").GetInt32());
            listed.AddRange(orderPage.GetProperty("items").EnumerateArray());
        }

        Dictionary<string, JsonElement> orderByNo = listed.ToDictionary(order => order.GetProperty("orderNo").GetString()!);
        Assert.Equal(plant.Orders.Count, orderByNo.Count);
        foreach (GeneratedOrder order in plant.Orders)
        {
            JsonElement detail = await viewer.GetJsonAsync(OrderPath(orderByNo[order.OrderNo].GetProperty("id").GetInt32()));
            Assert.Equal(
                (order.CustomerName, order.ProductCode, order.Quantity, order.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), order.Priority.ToString()),
                (detail.GetProperty("customerName").GetString()!,
                    detail.GetProperty("product").GetProperty("code").GetString()!,
                    detail.GetProperty("quantity").GetInt32(),
                    detail.GetProperty("dueDate").GetString()!,
                    detail.GetProperty("priority").GetString()!));
            JsonElement[] operations = [.. detail.GetProperty("operations").EnumerateArray()];
            Assert.Equal(productByCode[order.ProductCode].Stages.Count, operations.Length);
            Assert.All(operations, operation => Assert.Equal(1, operation.GetProperty("lots").GetArrayLength()));
        }
    }

    // Generated starts never fall into a DST gap or a repeated hour (A33), so the zone converts
    // them without choosing; the loader itself converts through the engine.
    private static async Task AssertUnavailabilitiesAsync(ApiSession viewer, GeneratedPlant plant)
    {
        TimeZoneInfo plantZone = TimeZoneInfo.FindSystemTimeZoneById(SchedulerApiFactory.DefaultPlantTimeZone);
        JsonElement resources = await viewer.GetJsonAsync("/api/v1/resources");
        List<(string, string, DateTimeOffset, DateTimeOffset?, string)> saved = [];
        foreach (JsonElement resource in resources.EnumerateArray())
        {
            JsonElement unavailabilities = await viewer.GetJsonAsync($"/api/v1/resources/{resource.GetProperty("id").GetInt32()}/unavailabilities");
            saved.AddRange(unavailabilities.EnumerateArray().Select(unavailability => (
                resource.GetProperty("name").GetString()!,
                unavailability.GetProperty("kind").GetString()!,
                unavailability.GetProperty("start").GetDateTimeOffset(),
                unavailability.GetProperty("end").ValueKind == JsonValueKind.Null ? (DateTimeOffset?)null : unavailability.GetProperty("end").GetDateTimeOffset(),
                unavailability.GetProperty("reason").GetString()!)));
        }

        Assert.Equal(
            plant.Unavailabilities.Select(unavailability =>
            {
                DateTimeOffset start = new(TimeZoneInfo.ConvertTimeToUtc(unavailability.LocalStart, plantZone), TimeSpan.Zero);
                DateTimeOffset? end = unavailability.DurationMinutes is { } minutes ? start.AddMinutes(minutes) : null;
                return (unavailability.MachineName, unavailability.Kind.ToString(), start, end, unavailability.Reason);
            }).Order(),
            saved.Order());
    }

    private static string ShiftTime(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
}

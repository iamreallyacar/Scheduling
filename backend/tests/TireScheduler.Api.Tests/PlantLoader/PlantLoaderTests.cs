using System.Globalization;
using System.Text.Json;
using TireScheduler.Api.Security;
using TireScheduler.PlantGenerator;
using TireScheduler.PlantLoader;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.PlantLoader;

// The loader against a running API on an empty database of its own (DEC-32).
public sealed class PlantLoaderTests(PostgresFixture postgres)
{
    private static readonly DateOnly StartMonday = new(2026, 11, 16);

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
        Assert.Contains($"{plant.Unavailabilities.Count} unavailabilities not loaded", output, StringComparison.Ordinal);
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);
        await AssertMachinesAsync(viewer, plant);
        await AssertCalendarAsync(viewer, plant);
        await AssertProductsAsync(viewer, plant);
        await AssertOrdersAsync(viewer, plant);
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

    private static LoaderSettings AdminSettings(string presetName, PlantParameters parameters, int seed) =>
        new(presetName, parameters, seed, StartMonday, new Uri("https://localhost"), SchedulerApiFactory.AdminEmail, SchedulerApiFactory.AdminPassword);

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

    private static string ShiftTime(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
}

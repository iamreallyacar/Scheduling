using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;

namespace TireScheduler.Api.Tests.TestData;

// Scheduling rows built through the API, as a Planner would; one method per kind of row.
internal static class SchedulingTestData
{
    public const string LiveVersion = "live";

    public static string UnscheduledLotsPath(string versionId) => $"/api/v1/versions/{versionId}/unscheduled-lots";

    public static string ConflictsPath(string versionId) => $"/api/v1/versions/{versionId}/conflicts";

    public static string TimelinePath(string versionId, string query) => $"/api/v1/versions/{versionId}/timeline?{query}";

    public static string PlacementPath(string versionId, int lotId) => $"/api/v1/versions/{versionId}/lots/{lotId}/placement";

    public static string UnavailabilitiesPath(int resourceId) => $"{MasterDataTestData.ResourcesPath}/{resourceId}/unavailabilities";

    public static string UnavailabilityPath(int resourceId, int unavailabilityId) => $"{UnavailabilitiesPath(resourceId)}/{unavailabilityId}";

    public static object Unavailability(string kind, DateTimeOffset start, DateTimeOffset? end, string? reason = null) =>
        new { kind, start, end, reason };

    public static async Task<int> CreateUnavailabilityAsync(ApiSession planner, int resourceId, object unavailability)
    {
        JsonElement created = await ApiSession.ReadJsonAsync(await planner.PostAsync(UnavailabilitiesPath(resourceId), unavailability));
        return created.GetProperty("id").GetInt32();
    }

    public static object Placement(int resourceId, DateTimeOffset plannedStart) => new { resourceId, plannedStart };

    // Places the lot in the live version and answers the saved placement.
    public static async Task<JsonElement> PlaceLotAsync(ApiSession planner, int lotId, int resourceId, DateTimeOffset plannedStart)
    {
        HttpResponseMessage response = await planner.PutAsync(PlacementPath(LiveVersion, lotId), Placement(resourceId, plannedStart));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Placing lot {lotId} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return await ApiSession.ReadJsonAsync(response);
    }

    public sealed record TestPlant
    {
        public required int BuildingTypeId { get; init; }

        public required int CuringTypeId { get; init; }

        public required int ProductId { get; init; }

        public required int Builder1 { get; init; }

        public required int Press1 { get; init; }

        public required int Press2 { get; init; }
    }

    // Plant-local times of the week of Mon 2026-11-16 in Berlin (UTC+1), as the UTC instants the
    // API and the database take.
    public static DateTimeOffset Berlin(int day, int hour) => new DateTimeOffset(2026, 11, day, hour, 0, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    // Building, then curing with no lag, each on machine types of its own (acceptance-criteria.md section 4).
    public static async Task<TestPlant> CreatePlantAsync(SchedulerApiFactory apiFactory, string prefix)
    {
        using ApiSession admin = await apiFactory.SignInAsRoleAsync(UserRole.Admin);
        int buildingTypeId = await CreateResourceTypeAsync(admin, $"{prefix} building");
        int curingTypeId = await CreateResourceTypeAsync(admin, $"{prefix} curing");
        int productId = await CreateProductAsync(admin, prefix);
        await SaveRoutingAsync(admin, productId, [Stage(1, buildingTypeId, cycleMinutes: 12), Stage(2, curingTypeId, cycleMinutes: 24)], [Dependency(1, 2)]);
        return new TestPlant
        {
            BuildingTypeId = buildingTypeId,
            CuringTypeId = curingTypeId,
            ProductId = productId,
            Builder1 = await CreateResourceAsync(admin, $"{prefix} Builder 1", buildingTypeId),
            Press1 = await CreateResourceAsync(admin, $"{prefix} Press 1", curingTypeId),
            Press2 = await CreateResourceAsync(admin, $"{prefix} Press 2", curingTypeId),
        };
    }

    // Scenarios have no API before F-SCH-12, so the scenario and its placement are written directly.
    public static async Task<int> AddScenarioPlacementAsync(SchedulerApiFactory apiFactory, int lotId, int resourceId, DateTimeOffset start, DateTimeOffset end)
    {
        await using AsyncServiceScope scope = apiFactory.Services.CreateAsyncScope();
        SchedulingDbContext context = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        ScheduleVersion scenario = new() { Name = $"Scenario for lot {lotId}", IsLive = false };
        context.ScheduleVersions.Add(scenario);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.Placements.Add(new Placement { VersionId = scenario.Id, LotId = lotId, ResourceId = resourceId, PlannedStart = start, PlannedEnd = end });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return scenario.Id;
    }
}

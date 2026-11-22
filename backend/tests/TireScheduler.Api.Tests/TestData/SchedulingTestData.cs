using System.Globalization;
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

    // Without resourceId and notBefore the search covers every active machine of the lot's type from now.
    public static string EarliestSlotPath(string versionId, int lotId, int? resourceId, DateTimeOffset? notBefore)
    {
        List<string> query = [];
        if (resourceId is { } id)
        {
            query.Add(string.Create(CultureInfo.InvariantCulture, $"resourceId={id}"));
        }

        if (notBefore is { } instant)
        {
            query.Add($"notBefore={Uri.EscapeDataString(instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}");
        }

        string path = $"/api/v1/versions/{versionId}/lots/{lotId}/earliest-slot";
        return query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
    }

    public static string AsapProposalPath(string versionId, int orderId, DateTimeOffset? notBefore)
    {
        string path = $"/api/v1/versions/{versionId}/orders/{orderId}/asap-proposal";
        return notBefore is { } instant
            ? $"{path}?notBefore={Uri.EscapeDataString(instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}"
            : path;
    }

    public static string AsapCommitPath(string versionId, int orderId) => $"/api/v1/versions/{versionId}/orders/{orderId}/asap-proposal/commit";

    // The commit body of a proposal, sent unchanged: its start and its proposed lots (AC-200).
    public static object CommitBodyOf(JsonElement proposal) => new
    {
        notBefore = proposal.GetProperty("notBefore").GetDateTimeOffset(),
        placements = proposal.GetProperty("lots").EnumerateArray()
            .Where(lot => lot.GetProperty("isProposed").GetBoolean())
            .Select(lot => new
            {
                lotId = lot.GetProperty("lotId").GetInt32(),
                resourceId = lot.GetProperty("resource").GetProperty("id").GetInt32(),
                plannedStart = lot.GetProperty("plannedStart").GetDateTimeOffset(),
            })
            .ToArray(),
    };

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

    // The test plant's orders are of this quantity, so each stage has one lot of it. With the cycle
    // times of CreatePlantAsync a building lot takes 2 hours and a curing lot 4 hours.
    public const int LotQuantity = 10;

    private const int BuildingCycleMinutes = 12;
    private const int CuringCycleMinutes = 24;

    public const int BuildingLotMinutes = LotQuantity * BuildingCycleMinutes;

    public const int CuringLotMinutes = LotQuantity * CuringCycleMinutes;

    public static (int ResourceId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd) PlacementValues(JsonElement placement) =>
        (placement.GetProperty("resourceId").GetInt32(), placement.GetProperty("plannedStart").GetDateTimeOffset(), placement.GetProperty("plannedEnd").GetDateTimeOffset());

    // The lot ids of an unscheduled-lots answer, in its order.
    public static int[] QueuedLotIds(JsonElement queue) => [.. queue.EnumerateArray().Select(lot => lot.GetProperty("id").GetInt32())];

    public sealed record TestPlant
    {
        public required int BuildingTypeId { get; init; }

        public required int CuringTypeId { get; init; }

        public required int ProductId { get; init; }

        public required int Builder1 { get; init; }

        public required int Press1 { get; init; }

        public required int Press2 { get; init; }
    }

    // Building, then curing with no lag, each on machine types of its own (acceptance-criteria.md section 4).
    public static async Task<TestPlant> CreatePlantAsync(SchedulerApiFactory apiFactory, string prefix)
    {
        using ApiSession admin = await apiFactory.SignInAsRoleAsync(UserRole.Admin);
        int buildingTypeId = await CreateResourceTypeAsync(admin, $"{prefix} building");
        int curingTypeId = await CreateResourceTypeAsync(admin, $"{prefix} curing");
        int productId = await CreateProductAsync(admin, prefix);
        await SaveRoutingAsync(admin, productId, [Stage(1, buildingTypeId, cycleMinutes: BuildingCycleMinutes), Stage(2, curingTypeId, cycleMinutes: CuringCycleMinutes)], [Dependency(1, 2)]);
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

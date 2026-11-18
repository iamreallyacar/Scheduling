using System.Net;
using System.Text.Json;

namespace TireScheduler.Api.Tests.TestData;

// Scheduling rows built through the API, as a Planner would; one method per kind of row.
internal static class SchedulingTestData
{
    public const string LiveVersion = "live";

    public static string UnscheduledLotsPath(string versionId) => $"/api/v1/versions/{versionId}/unscheduled-lots";

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
}

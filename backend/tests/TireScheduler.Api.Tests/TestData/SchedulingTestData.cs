using System.Text.Json;

namespace TireScheduler.Api.Tests.TestData;

// Scheduling rows built through the API, as a Planner would; one method per kind of row.
internal static class SchedulingTestData
{
    public static string UnscheduledLotsPath(string versionId) => $"/api/v1/versions/{versionId}/unscheduled-lots";

    public static string UnavailabilitiesPath(int resourceId) => $"{MasterDataTestData.ResourcesPath}/{resourceId}/unavailabilities";

    public static string UnavailabilityPath(int resourceId, int unavailabilityId) => $"{UnavailabilitiesPath(resourceId)}/{unavailabilityId}";

    public static object Unavailability(string kind, DateTimeOffset start, DateTimeOffset? end, string? reason = null) =>
        new { kind, start, end, reason };

    public static async Task<int> CreateUnavailabilityAsync(ApiSession planner, int resourceId, object unavailability)
    {
        JsonElement created = await ApiSession.ReadJsonAsync(await planner.PostAsync(UnavailabilitiesPath(resourceId), unavailability));
        return created.GetProperty("id").GetInt32();
    }
}

using System.Net;
using System.Text.Json;

namespace TireScheduler.Api.Tests.TestData;

// Lot progress recorded through the API, as a Planner would.
internal static class ProgressTestData
{
    public static string ProgressPath(int lotId) => $"/api/v1/lots/{lotId}/progress";

    public static object ProgressBody(DateTimeOffset actualStart, DateTimeOffset? actualEnd = null) => new { actualStart, actualEnd };

    // Records the progress and answers the saved progress.
    public static async Task<JsonElement> RecordProgressAsync(ApiSession planner, int lotId, DateTimeOffset actualStart, DateTimeOffset? actualEnd = null)
    {
        HttpResponseMessage response = await planner.PutAsync(ProgressPath(lotId), ProgressBody(actualStart, actualEnd));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Recording the progress of lot {lotId} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return await ApiSession.ReadJsonAsync(response);
    }
}

using System.Net;
using System.Text.Json;

namespace TireScheduler.Api.Tests;

internal static class ProblemAssert
{
    // "Rejected" in acceptance-criteria.md section 1: problem details with the given status and code.
    public static async Task<JsonElement> HasProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(expectedStatus == response.StatusCode, $"Expected {expectedStatus} but got {response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement problem = document.RootElement.Clone();
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.NotEmpty(problem.GetProperty("detail").GetString()!);
        return problem;
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TireScheduler.PlantLoader;

// Talks to /api/v1 the way the frontend does (API.md section "Sign-in and anti-forgery"): one
// session cookie, kept by the HttpClient's handler, and the anti-forgery token on every write.
// Every answer that is not a success stops the loader (AC-159).
internal sealed class SchedulerApiClient(HttpClient client)
{
    private const string ApiBase = "/api/v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private (string HeaderName, string Token)? antiforgery;

    public async Task SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        await SendAsync(HttpMethod.Post, "/auth/sign-in", new SignInRequest(email, password), cancellationToken);
        // The token belongs to the session, so it is fetched again after sign-in.
        antiforgery = null;
    }

    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, ApiUri(path));
        using HttpResponseMessage response = await SendCheckedAsync(request, cancellationToken);
        return await ReadAsync<T>(response, request, cancellationToken);
    }

    public async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = await NewWriteAsync(method, path, body, cancellationToken);
        using HttpResponseMessage response = await SendCheckedAsync(request, cancellationToken);
        return await ReadAsync<T>(response, request, cancellationToken);
    }

    public async Task SendAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = await NewWriteAsync(method, path, body, cancellationToken);
        using HttpResponseMessage response = await SendCheckedAsync(request, cancellationToken);
    }

    private async Task<HttpRequestMessage> NewWriteAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        antiforgery ??= await FetchAntiforgeryTokenAsync(cancellationToken);
        HttpRequestMessage request = new(method, ApiUri(path)) { Content = JsonContent.Create(body, body.GetType(), options: Json) };
        request.Headers.Add(antiforgery.Value.HeaderName, antiforgery.Value.Token);
        return request;
    }

    private async Task<(string HeaderName, string Token)> FetchAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        AntiforgeryTokenResponse token = await GetAsync<AntiforgeryTokenResponse>("/auth/antiforgery-token", cancellationToken);
        return (token.HeaderName, token.Token);
    }

    private async Task<HttpResponseMessage> SendCheckedAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Read before sending, because sending makes the address absolute.
        string target = $"{request.Method.Method} {request.RequestUri!.OriginalString}";
        HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            ProblemResponse? problem = ReadProblem(body);
            throw new ApiRejectedException(target, (int)response.StatusCode, problem?.Code, problem?.Detail ?? NullIfEmpty(body));
        }
    }

    // An answer that is not problem details is shown as it came.
    private static ProblemResponse? ReadProblem(string body)
    {
        try
        {
            return body.Length == 0 ? null : JsonSerializer.Deserialize<ProblemResponse>(body, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpRequestMessage request, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new LoaderException($"{request.Method} {request.RequestUri} answered without a body.");

    private static Uri ApiUri(string path) => new(ApiBase + path, UriKind.Relative);

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
}

using System.Net.Http.Json;
using System.Text.Json;

namespace TireScheduler.Api.Tests;

// Does what the browser and the frontend client do: keeps cookies between requests and sends
// the antiforgery token with state-changing requests.
internal sealed class ApiSession(HttpClient client) : IDisposable
{
    private readonly Dictionary<string, string> cookies = [];
    private (string HeaderName, string Token)? antiforgery;

    public string CookieHeader => string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));

    public void Dispose() => client.Dispose();

    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path);

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        HttpResponseMessage response = await GetAsync(path);
        return await ReadJsonAsync(response);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}: {body}");
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    public async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        HttpResponseMessage response = await PostAsync("/api/v1/auth/sign-in", new { email, password });
        // The token belongs to the session, so the next state-changing request fetches a new one.
        antiforgery = null;
        return response;
    }

    public async Task<HttpResponseMessage> SignOutAsync()
    {
        HttpResponseMessage response = await PostAsync("/api/v1/auth/sign-out");
        antiforgery = null;
        return response;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, bool withAntiforgeryToken = true, string? cookieHeader = null)
    {
        using HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = body as HttpContent ?? JsonContent.Create(body);
        }

        if (withAntiforgeryToken && method != HttpMethod.Get)
        {
            antiforgery ??= await FetchAntiforgeryTokenAsync();
            request.Headers.Add(antiforgery.Value.HeaderName, antiforgery.Value.Token);
        }

        return await SendWithCookiesAsync(request, cookieHeader ?? CookieHeader);
    }

    private async Task<(string HeaderName, string Token)> FetchAntiforgeryTokenAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/v1/auth/antiforgery-token", UriKind.Relative));
        JsonElement tokenResponse = await ReadJsonAsync(await SendWithCookiesAsync(request, CookieHeader));
        return (tokenResponse.GetProperty("headerName").GetString()!, tokenResponse.GetProperty("token").GetString()!);
    }

    private async Task<HttpResponseMessage> SendWithCookiesAsync(HttpRequestMessage request, string cookieHeader)
    {
        if (cookieHeader.Length > 0)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookieHeaders))
        {
            foreach (string setCookie in setCookieHeaders)
            {
                StoreCookie(setCookie);
            }
        }

        return response;
    }

    // "name=value; attributes". The server deletes a cookie by sending it with an empty value.
    private void StoreCookie(string setCookieHeader)
    {
        string[] nameAndValue = setCookieHeader.Split(';')[0].Split('=', 2);
        if (nameAndValue[1].Length == 0)
        {
            cookies.Remove(nameAndValue[0]);
        }
        else
        {
            cookies[nameAndValue[0]] = nameAndValue[1];
        }
    }
}

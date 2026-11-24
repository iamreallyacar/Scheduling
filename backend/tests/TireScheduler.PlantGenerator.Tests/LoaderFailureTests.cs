using System.Net;
using System.Text;
using TireScheduler.PlantLoader;

namespace TireScheduler.PlantGenerator.Tests;

public sealed class LoaderFailureTests
{
    private static readonly Dictionary<string, string> SignInSettings = new()
    {
        [LoaderCommandLine.EmailVariable] = "admin@plant.test",
        [LoaderCommandLine.PasswordVariable] = "First-Admin-1",
    };

    private static readonly DateOnly Wednesday = new(2026, 11, 18);

    [Fact]
    public async Task AC_226_a_request_that_runs_past_the_timeout_stops_the_loader_with_a_message()
    {
        using HttpClient client = new(new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The delay only ends by cancellation.");
        }))
        {
            BaseAddress = new Uri("https://api.test"),
        };

        (int exitCode, string errors) = await RunAsync(client, Settings() with { RequestTimeoutSeconds = 1 });

        Assert.Equal(1, exitCode);
        Assert.Contains("/api/v1/auth/", errors, StringComparison.Ordinal);
        Assert.Contains(LoaderCommandLine.RequestTimeoutVariable, errors, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text/html", "<html>Bad gateway</html>")]
    [InlineData("application/json", "{ not json")]
    public async Task AC_226_an_answer_that_is_not_json_stops_the_loader_with_a_message(string contentType, string body)
    {
        using HttpClient client = new(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        })))
        {
            BaseAddress = new Uri("https://api.test"),
        };

        (int exitCode, string errors) = await RunAsync(client, Settings());

        Assert.Equal(1, exitCode);
        Assert.Contains("/api/v1/auth/antiforgery-token", errors, StringComparison.Ordinal);
        Assert.Contains("not JSON", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC_226_parameters_that_fail_the_checks_stop_the_loader_before_any_request()
    {
        int requests = 0;
        using HttpClient client = new(new StubHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }))
        {
            BaseAddress = new Uri("https://api.test"),
        };

        (int exitCode, string errors) = await RunAsync(client, Settings() with { Parameters = PlantPresets.Small with { ProductCount = 0 } });

        Assert.Equal(1, exitCode);
        Assert.Contains("ProductCount", errors, StringComparison.Ordinal);
        Assert.Equal(0, requests);
    }

    [Fact]
    public void AC_227_without_the_variable_a_request_may_take_300_seconds()
    {
        Assert.Equal(300, Parse(SignInSettings).RequestTimeoutSeconds);
    }

    [Fact]
    public void AC_227_the_variable_sets_the_request_timeout()
    {
        Dictionary<string, string> environment = new(SignInSettings) { [LoaderCommandLine.RequestTimeoutVariable] = "45" };

        Assert.Equal(45, Parse(environment).RequestTimeoutSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("soon")]
    [InlineData("1.5")]
    [InlineData("2147484")]
    [InlineData("9999999999")]
    public void AC_227_a_timeout_that_is_not_a_whole_number_from_1_to_2147483_fails_naming_the_variable(string value)
    {
        Dictionary<string, string> environment = new(SignInSettings) { [LoaderCommandLine.RequestTimeoutVariable] = value };

        LoaderUsageException failure = Assert.Throws<LoaderUsageException>(() => Parse(environment));

        Assert.Contains(LoaderCommandLine.RequestTimeoutVariable, failure.Message, StringComparison.Ordinal);
    }

    private static LoaderSettings Parse(Dictionary<string, string> environment) =>
        LoaderCommandLine.Parse(["--preset", "small"], environment.GetValueOrDefault, Wednesday);

    private static LoaderSettings Settings() => Parse(SignInSettings);

    private static async Task<(int ExitCode, string Errors)> RunAsync(HttpClient client, LoaderSettings settings)
    {
        await using StringWriter output = new(System.Globalization.CultureInfo.InvariantCulture);
        await using StringWriter errors = new(System.Globalization.CultureInfo.InvariantCulture);
        int exitCode = await LoaderRun.RunAsync(settings, client, output, errors, TestContext.Current.CancellationToken);
        return (exitCode, errors.ToString());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => answer(request, cancellationToken);
    }
}

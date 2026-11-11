using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Tests.Errors;

// The error handling baseline (F-NFR-2) on its own: a small host with the same error handling
// setup as the API and test-only endpoints, so no database or session is involved.
public sealed class ErrorHandlingTests : IAsyncLifetime
{
    private const string SecretFailureText = "connection string with password";

    private readonly RecordingLoggerProvider recordedLogs = new();
    private WebApplication? host;
    private HttpClient? client;

    private HttpClient Client => client ?? throw new InvalidOperationException("The test host is not started.");

    public async ValueTask InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(recordedLogs);
        builder.Services.AddSchedulerErrorHandling();

        host = builder.Build();
        host.UseSchedulerErrorHandling();
        host.MapPost("/machines", (MachineRequest request) => Results.Ok(request)).WithRequestValidation<MachineRequest>();
        host.MapGet("/machines/{id:int}", IResult (int id) => throw ApiProblemException.NotFound($"Machine {id}"));
        host.MapGet("/failing", IResult () => throw new InvalidOperationException(SecretFailureText));

        await host.StartAsync(TestContext.Current.CancellationToken);
        client = host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (host is not null)
        {
            await host.DisposeAsync();
        }

        recordedLogs.Dispose();
    }

    [Fact]
    public async Task AC_21_every_invalid_field_is_reported_in_one_validation_failed_answer()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/machines", new { capacity = 0 }, TestContext.Current.CancellationToken);

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        JsonElement errors = problem.GetProperty("errors");
        Assert.Equal(["capacity", "name"], errors.EnumerateObject().Select(field => field.Name).Order());
        Assert.NotEmpty(errors.GetProperty("name")[0].GetString()!);
    }

    [Fact]
    public async Task AC_21_valid_request_reaches_the_endpoint()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/machines", new { name = "Press 1", capacity = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("{ \"name\": ")]
    [InlineData("{ \"name\": 5, \"capacity\": 2 }")]
    [InlineData("{ \"name\": \"Press 1\", \"capacity\": \"2\" }")]
    public async Task AC_22_unreadable_json_or_wrong_value_types_are_request_malformed(string body)
    {
        using StringContent content = new(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await Client.PostAsync(new Uri("/machines", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.RequestMalformed);
    }

    [Theory]
    [InlineData("/machines/7")]
    [InlineData("/no-such-route")]
    public async Task AC_23_unknown_id_or_route_is_not_found(string path)
    {
        HttpResponseMessage response = await Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_25_unexpected_error_answers_generic_500_and_logs_details_under_the_same_correlation_id()
    {
        HttpResponseMessage response = await Client.GetAsync(new Uri("/failing", UriKind.Relative), TestContext.Current.CancellationToken);

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.InternalServerError, ProblemCodes.ServerError);
        string correlationId = problem.GetProperty("correlationId").GetString()!;
        Assert.NotEmpty(correlationId);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(SecretFailureText, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);

        RecordedLogEntry logEntry = Assert.Single(recordedLogs.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
        Assert.Contains(correlationId, logEntry.Message, StringComparison.Ordinal);
        Assert.Equal(SecretFailureText, logEntry.Exception!.Message);
    }

    private sealed record MachineRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;

        [Range(1, 10)]
        public int Capacity { get; init; }
    }
}

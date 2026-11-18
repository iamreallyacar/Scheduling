using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.Auth;

// F-AUTH-1 through the API.
public sealed class SignInTests(SchedulerApiFixture api, PostgresFixture postgres) : IClassFixture<SchedulerApiFixture>
{
    private const string WrongPassword = "Wrong-Password-1";

    [Fact]
    public async Task AC_1_sign_in_sets_a_protected_session_cookie_and_me_returns_name_and_role()
    {
        using ApiSession session = api.Factory.NewSession();

        HttpResponseMessage signInResponse = await session.SignInAsync(SchedulerApiFactory.AdminEmail, SchedulerApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        string sessionCookie = Assert.Single(signInResponse.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("tire-scheduler-session=", StringComparison.Ordinal));
        string[] cookieAttributes = sessionCookie.Split("; ").Skip(1).ToArray();
        Assert.Contains("httponly", cookieAttributes);
        Assert.Contains("secure", cookieAttributes);
        Assert.Contains("samesite=lax", cookieAttributes);

        JsonElement me = await session.GetJsonAsync("/api/v1/me");
        Assert.Equal("Administrator", me.GetProperty("name").GetString());
        Assert.Equal("Admin", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task AC_138_sign_in_works_over_plain_http_only_when_the_development_setting_allows_it()
    {
        await using SchedulerApiFactory developmentLike = new(postgres.NewDatabaseConnectionString(), cookiesRequireHttps: false);
        using ApiSession session = developmentLike.NewSession(overHttps: false);

        HttpResponseMessage signInResponse = await session.SignInAsync(SchedulerApiFactory.AdminEmail, SchedulerApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.All(signInResponse.Headers.GetValues("Set-Cookie"), cookie => Assert.DoesNotContain("secure", cookie.Split("; ")));
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/me")).StatusCode);

        // With the default, a state-changing request over plain http is refused outright.
        using ApiSession insecureSession = api.Factory.NewSession(overHttps: false);
        HttpResponseMessage refused = await insecureSession.SendAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email = SchedulerApiFactory.AdminEmail, password = SchedulerApiFactory.AdminPassword },
            withAntiforgeryToken: false);
        await ProblemAssert.HasProblemAsync(refused, HttpStatusCode.InternalServerError, ProblemCodes.ServerError);
    }

    [Fact]
    public async Task AC_2_wrong_password_and_unknown_email_get_the_same_invalid_credentials_answer()
    {
        using ApiSession session = api.Factory.NewSession();

        JsonElement wrongPassword = await ProblemAssert.HasProblemAsync(
            await session.SignInAsync(SchedulerApiFactory.AdminEmail, WrongPassword), HttpStatusCode.Unauthorized, ProblemCodes.AuthInvalidCredentials);
        JsonElement unknownEmail = await ProblemAssert.HasProblemAsync(
            await session.SignInAsync("nobody@plant.test", WrongPassword), HttpStatusCode.Unauthorized, ProblemCodes.AuthInvalidCredentials);

        Assert.Equal(WithoutTraceId(wrongPassword), WithoutTraceId(unknownEmail));
    }

    [Fact]
    public async Task AC_3_correct_password_is_locked_out_after_five_failures_and_works_again_after_fifteen_minutes()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero));
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), clock: clock);
        using ApiSession session = factory.NewSession();
        for (int failedAttempt = 1; failedAttempt <= 5; failedAttempt++)
        {
            await ProblemAssert.HasProblemAsync(
                await session.SignInAsync(SchedulerApiFactory.AdminEmail, WrongPassword), HttpStatusCode.Unauthorized, ProblemCodes.AuthInvalidCredentials);
        }

        clock.Advance(TimeSpan.FromMinutes(14));
        await ProblemAssert.HasProblemAsync(
            await session.SignInAsync(SchedulerApiFactory.AdminEmail, SchedulerApiFactory.AdminPassword), HttpStatusCode.Unauthorized, ProblemCodes.AuthLockedOut);

        clock.Advance(TimeSpan.FromMinutes(1));
        HttpResponseMessage afterLockout = await session.SignInAsync(SchedulerApiFactory.AdminEmail, SchedulerApiFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, afterLockout.StatusCode);
    }

    [Fact]
    public async Task AC_4_inactive_user_is_rejected_like_a_wrong_password()
    {
        const string inactiveEmail = "inactive@plant.test";
        await api.Factory.EnsureUserAsync(inactiveEmail, UserRole.Planner, isActive: false);
        using ApiSession session = api.Factory.NewSession();

        JsonElement inactiveUser = await ProblemAssert.HasProblemAsync(
            await session.SignInAsync(inactiveEmail, SchedulerApiFactory.UserPassword), HttpStatusCode.Unauthorized, ProblemCodes.AuthInvalidCredentials);
        JsonElement wrongPassword = await ProblemAssert.HasProblemAsync(
            await session.SignInAsync(SchedulerApiFactory.AdminEmail, WrongPassword), HttpStatusCode.Unauthorized, ProblemCodes.AuthInvalidCredentials);

        Assert.Equal(WithoutTraceId(wrongPassword), WithoutTraceId(inactiveUser));
    }

    [Fact]
    public async Task AC_5_sign_out_clears_the_cookie_and_the_old_cookie_no_longer_works()
    {
        // Its own user: signing out ends every session of that user (A21).
        const string email = "signs-out@plant.test";
        await api.Factory.EnsureUserAsync(email, UserRole.Viewer);
        using ApiSession session = await api.Factory.SignInAsync(email, SchedulerApiFactory.UserPassword);
        string cookieBeforeSignOut = session.CookieHeader;

        HttpResponseMessage signOutResponse = await session.SignOutAsync();

        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);
        Assert.DoesNotContain("tire-scheduler-session", session.CookieHeader, StringComparison.Ordinal);
        HttpResponseMessage withOldCookie = await session.SendAsync(HttpMethod.Get, "/api/v1/me", cookieHeader: cookieBeforeSignOut);
        await ProblemAssert.HasProblemAsync(withOldCookie, HttpStatusCode.Unauthorized, ProblemCodes.AuthRequired);
    }

    [Theory]
    [InlineData("GET", "/api/v1/me")]
    [InlineData("POST", "/api/v1/auth/sign-out")]
    public async Task AC_6_request_without_a_session_is_auth_required(string method, string path)
    {
        using ApiSession session = api.Factory.NewSession();

        HttpResponseMessage response = await session.SendAsync(new HttpMethod(method), path, withAntiforgeryToken: false);

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Unauthorized, ProblemCodes.AuthRequired);
    }

    [Fact]
    public async Task AC_7_state_changing_request_without_antiforgery_token_is_rejected_and_changes_nothing()
    {
        using ApiSession session = await api.Factory.SignInAsRoleAsync(UserRole.Planner);

        HttpResponseMessage response = await session.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-out", withAntiforgeryToken: false);

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.AuthAntiforgery);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task AC_7_sign_in_without_antiforgery_token_is_rejected()
    {
        using ApiSession session = api.Factory.NewSession();

        HttpResponseMessage response = await session.SendAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email = SchedulerApiFactory.AdminEmail, password = SchedulerApiFactory.AdminPassword },
            withAntiforgeryToken: false);

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.AuthAntiforgery);
    }

    [Fact]
    public async Task AC_21_sign_in_without_email_and_password_reports_both_fields()
    {
        using ApiSession session = api.Factory.NewSession();

        HttpResponseMessage response = await session.PostAsync("/api/v1/auth/sign-in", new { });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["email", "password"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order());
    }

    private static string WithoutTraceId(JsonElement problem) =>
        string.Join("|", problem.EnumerateObject().Where(field => field.Name != "traceId").Select(field => $"{field.Name}={field.Value}"));
}

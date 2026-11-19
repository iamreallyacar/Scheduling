using System.Net;
using System.Text.Json;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.PlantTime;
using static TireScheduler.Api.Tests.TestData.SchedulingTestData;

namespace TireScheduler.Api.Tests.Scheduling;

// F-MNT-1 through the API: recording, reading, changing and deleting unavailabilities.
public sealed class UnavailabilityTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    private static readonly DateTimeOffset TuesdaySix = Berlin(17, 6);

    [Fact]
    public async Task AC_11_AC_166_planner_records_planned_maintenance_and_an_open_breakdown_on_an_inactive_machine()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateMachineAsync(admin, "AC-166 press");
        await admin.PutAsync($"{ResourcesPath}/{resourceId}", new { name = "AC-166 press", resourceTypeId = await TypeIdAsync(admin, resourceId), isActive = false });
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        DateTimeOffset startInBerlinOffset = TuesdaySix.ToOffset(TimeSpan.FromHours(1));

        HttpResponseMessage planned = await planner.PostAsync(
            UnavailabilitiesPath(resourceId),
            Unavailability("Planned", startInBerlinOffset, startInBerlinOffset.AddHours(4), "  Mold change "));
        HttpResponseMessage openBreakdown = await planner.PostAsync(UnavailabilitiesPath(resourceId), Unavailability("Breakdown", TuesdaySix.AddHours(1), end: null));

        Assert.Equal(HttpStatusCode.Created, planned.StatusCode);
        JsonElement plannedBody = await ApiSession.ReadJsonAsync(planned);
        int plannedId = plannedBody.GetProperty("id").GetInt32();
        Assert.Equal($"/api/v1/resources/{resourceId}/unavailabilities/{plannedId}", planned.Headers.Location?.OriginalString);
        Assert.Equal(resourceId, plannedBody.GetProperty("resourceId").GetInt32());
        Assert.Equal("Planned", plannedBody.GetProperty("kind").GetString());
        AssertUtc(TuesdaySix, plannedBody.GetProperty("start"));
        AssertUtc(TuesdaySix.AddHours(4), plannedBody.GetProperty("end"));
        Assert.Equal("Mold change", plannedBody.GetProperty("reason").GetString());

        // Overlaps the planned maintenance, which is allowed (A37).
        Assert.Equal(HttpStatusCode.Created, openBreakdown.StatusCode);
        JsonElement breakdownBody = await ApiSession.ReadJsonAsync(openBreakdown);
        Assert.Equal("Breakdown", breakdownBody.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, breakdownBody.GetProperty("end").ValueKind);
        Assert.Equal(JsonValueKind.Null, breakdownBody.GetProperty("reason").ValueKind);
    }

    [Fact]
    public async Task AC_167_missing_and_invalid_values_are_reported_by_field()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateMachineAsync(admin, "AC-167 press");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        string path = UnavailabilitiesPath(resourceId);

        HttpResponseMessage empty = await planner.PostAsync(path, new { });
        HttpResponseMessage endNotAfterStart = await planner.PostAsync(path, Unavailability("Breakdown", TuesdaySix, TuesdaySix));
        HttpResponseMessage plannedWithoutEnd = await planner.PostAsync(path, Unavailability("Planned", TuesdaySix, end: null));
        HttpResponseMessage notWholeMinutes = await planner.PostAsync(path, Unavailability("Planned", TuesdaySix.AddSeconds(30), TuesdaySix.AddHours(1).AddMilliseconds(1)));
        HttpResponseMessage longReason = await planner.PostAsync(path, Unavailability("Planned", TuesdaySix, TuesdaySix.AddHours(1), new string('r', 201)));
        HttpResponseMessage unknownKind = await planner.PostAsync(path, Unavailability("Repair", TuesdaySix, TuesdaySix.AddHours(1)));

        Assert.Equal(["kind", "start"], await FieldsAsync(empty));
        Assert.Equal(["end"], await FieldsAsync(endNotAfterStart));
        Assert.Equal(["end"], await FieldsAsync(plannedWithoutEnd));
        Assert.Equal(["end", "start"], await FieldsAsync(notWholeMinutes));
        Assert.Equal(["reason"], await FieldsAsync(longReason));
        await ProblemAssert.HasProblemAsync(unknownKind, HttpStatusCode.BadRequest, ProblemCodes.RequestMalformed);
        Assert.Equal(0, (await planner.GetJsonAsync(path)).GetArrayLength());
    }

    [Fact]
    public async Task AC_168_the_list_holds_only_the_machines_own_unavailabilities_latest_start_first()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateMachineAsync(admin, "AC-168 press 1");
        int otherResourceId = await CreateMachineAsync(admin, "AC-168 press 2");
        int earlierId = await CreateUnavailabilityAsync(admin, resourceId, Unavailability("Planned", TuesdaySix, TuesdaySix.AddHours(2)));
        int laterId = await CreateUnavailabilityAsync(admin, resourceId, Unavailability("Breakdown", TuesdaySix.AddDays(1), end: null));
        await CreateUnavailabilityAsync(admin, otherResourceId, Unavailability("Breakdown", TuesdaySix.AddHours(1), end: null));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        JsonElement listed = await viewer.GetJsonAsync(UnavailabilitiesPath(resourceId));

        Assert.Equal([laterId, earlierId], listed.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()));
        await ProblemAssert.HasProblemAsync(await viewer.GetAsync(UnavailabilitiesPath(987654)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_169_planner_gives_an_open_breakdown_its_end_and_changes_are_checked_like_new_ones()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateMachineAsync(admin, "AC-169 press");
        int otherResourceId = await CreateMachineAsync(admin, "AC-169 other press");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int breakdownId = await CreateUnavailabilityAsync(planner, resourceId, Unavailability("Breakdown", TuesdaySix, end: null, "Leak"));

        HttpResponseMessage ended = await planner.PutAsync(UnavailabilityPath(resourceId, breakdownId), Unavailability("Breakdown", TuesdaySix, TuesdaySix.AddHours(3), "Leak fixed"));
        HttpResponseMessage plannedWithoutEnd = await planner.PutAsync(UnavailabilityPath(resourceId, breakdownId), Unavailability("Planned", TuesdaySix, end: null));
        HttpResponseMessage underOtherResource = await planner.PutAsync(UnavailabilityPath(otherResourceId, breakdownId), Unavailability("Breakdown", TuesdaySix, end: null));

        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        JsonElement endedBody = await ApiSession.ReadJsonAsync(ended);
        AssertUtc(TuesdaySix.AddHours(3), endedBody.GetProperty("end"));
        Assert.Equal("Leak fixed", endedBody.GetProperty("reason").GetString());
        Assert.Equal(["end"], await FieldsAsync(plannedWithoutEnd));
        await ProblemAssert.HasProblemAsync(underOtherResource, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        JsonElement stored = Assert.Single((await planner.GetJsonAsync(UnavailabilitiesPath(resourceId))).EnumerateArray());
        Assert.Equal("Breakdown", stored.GetProperty("kind").GetString());
        AssertUtc(TuesdaySix.AddHours(3), stored.GetProperty("end"));
    }

    [Fact]
    public async Task AC_170_planner_deletes_an_unavailability_and_its_id_is_then_not_found()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateMachineAsync(admin, "AC-170 press");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int unavailabilityId = await CreateUnavailabilityAsync(planner, resourceId, Unavailability("Breakdown", TuesdaySix, end: null));

        HttpResponseMessage deleted = await planner.DeleteAsync(UnavailabilityPath(resourceId, unavailabilityId));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, (await planner.GetJsonAsync(UnavailabilitiesPath(resourceId))).GetArrayLength());
        await ProblemAssert.HasProblemAsync(await planner.DeleteAsync(UnavailabilityPath(resourceId, unavailabilityId)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(
            await planner.PutAsync(UnavailabilityPath(resourceId, unavailabilityId), Unavailability("Breakdown", TuesdaySix, end: null)),
            HttpStatusCode.NotFound,
            ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_23_an_unavailability_of_a_missing_resource_is_not_found()
    {
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);

        HttpResponseMessage response = await planner.PostAsync(UnavailabilitiesPath(987654), Unavailability("Breakdown", TuesdaySix, end: null));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Fact]
    public async Task AC_20_a_resource_with_an_unavailability_cannot_be_deleted_until_the_unavailability_is_gone()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int resourceId = await CreateMachineAsync(admin, "AC-20 press with downtime");
        int unavailabilityId = await CreateUnavailabilityAsync(admin, resourceId, Unavailability("Planned", TuesdaySix, TuesdaySix.AddHours(4)));

        HttpResponseMessage whileInUse = await admin.DeleteAsync($"{ResourcesPath}/{resourceId}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(whileInUse, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        string detail = problem.GetProperty("detail").GetString()!;
        Assert.Contains("AC-20 press with downtime", detail, StringComparison.Ordinal);
        Assert.Contains("planned maintenance", detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{ResourcesPath}/{resourceId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(UnavailabilityPath(resourceId, unavailabilityId))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{ResourcesPath}/{resourceId}")).StatusCode);
    }

    private static async Task<int> CreateMachineAsync(ApiSession admin, string name) =>
        await CreateResourceAsync(admin, name, await CreateResourceTypeAsync(admin, $"{name} type"));

    private static async Task<int> TypeIdAsync(ApiSession session, int resourceId) =>
        (await session.GetJsonAsync($"{ResourcesPath}/{resourceId}")).GetProperty("resourceType").GetProperty("id").GetInt32();

    private static async Task<string[]> FieldsAsync(HttpResponseMessage response)
    {
        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        return [.. problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order()];
    }

    private static void AssertUtc(DateTimeOffset expected, JsonElement actual)
    {
        DateTimeOffset value = actual.GetDateTimeOffset();
        Assert.Equal(expected, value);
        Assert.Equal(TimeSpan.Zero, value.Offset);
    }
}

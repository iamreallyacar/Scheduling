using System.Net;
using System.Text.Json;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.MasterData;

// F-MD-1 through the API, with the error cases of F-NFR-1 and F-NFR-2 that apply to it.
public sealed class MachineRegisterTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    private const string ResourceTypesPath = "/api/v1/resource-types";
    private const string ResourcesPath = "/api/v1/resources";

    [Fact]
    public async Task AC_27_resource_type_is_saved_with_kind_machine_and_any_other_kind_is_rejected()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage withoutKind = await admin.PostAsync(ResourceTypesPath, new { name = "AC-27 extruder" });
        HttpResponseMessage withMachineKind = await admin.PostAsync(ResourceTypesPath, new { name = "AC-27 mixer", kind = "Machine" });
        HttpResponseMessage withOtherKind = await admin.PostAsync(ResourceTypesPath, new { name = "AC-27 mold", kind = "Mold" });

        Assert.Equal(HttpStatusCode.Created, withoutKind.StatusCode);
        Assert.Equal("Machine", (await ApiSession.ReadJsonAsync(withoutKind)).GetProperty("kind").GetString());
        Assert.Equal(HttpStatusCode.Created, withMachineKind.StatusCode);
        JsonElement problem = await ProblemAssert.HasProblemAsync(withOtherKind, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(problem.GetProperty("errors").TryGetProperty("kind", out _));
        Assert.DoesNotContain("AC-27 mold", await NamesAsync(admin, ResourceTypesPath));
    }

    [Fact]
    public async Task AC_28_resource_is_saved_active_on_the_plant_default_calendar_and_has_no_status_field()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-28 press");

        HttpResponseMessage response = await admin.PostAsync(ResourcesPath, new { name = "  AC-28 press 1 ", resourceTypeId = typeId, status = "Down", isActive = false });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement resource = await ApiSession.ReadJsonAsync(response);
        Assert.Equal("AC-28 press 1", resource.GetProperty("name").GetString());
        Assert.True(resource.GetProperty("isActive").GetBoolean());
        Assert.Equal("Plant default", resource.GetProperty("calendar").GetProperty("name").GetString());
        Assert.Equal(typeId, resource.GetProperty("resourceType").GetProperty("id").GetInt32());
        Assert.False(resource.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task AC_29_admin_changes_name_type_and_active_flag()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int firstTypeId = await CreateResourceTypeAsync(admin, "AC-29 type A");
        int secondTypeId = await CreateResourceTypeAsync(admin, "AC-29 type B");
        int resourceId = await CreateResourceAsync(admin, "AC-29 machine", firstTypeId);

        HttpResponseMessage response = await admin.PutAsync($"{ResourcesPath}/{resourceId}", new { name = "AC-29 renamed", resourceTypeId = secondTypeId, isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement saved = await admin.GetJsonAsync($"{ResourcesPath}/{resourceId}");
        Assert.Equal("AC-29 renamed", saved.GetProperty("name").GetString());
        Assert.Equal(secondTypeId, saved.GetProperty("resourceType").GetProperty("id").GetInt32());
        Assert.False(saved.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task AC_30_resources_are_listed_by_type_then_name_and_filter_by_type_and_active_flag()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int buildingTypeId = await CreateResourceTypeAsync(admin, "AC-30 A building");
        int curingTypeId = await CreateResourceTypeAsync(admin, "AC-30 B curing");
        await CreateResourceAsync(admin, "AC-30 press 2", curingTypeId);
        await CreateResourceAsync(admin, "AC-30 press 1", curingTypeId);
        await CreateResourceAsync(admin, "AC-30 builder 1", buildingTypeId);
        int retiredId = await CreateResourceAsync(admin, "AC-30 builder 0", buildingTypeId);
        await admin.PutAsync($"{ResourcesPath}/{retiredId}", new { name = "AC-30 builder 0", resourceTypeId = buildingTypeId, isActive = false });
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        string[] all = (await NamesAsync(viewer, ResourcesPath)).Where(name => name.StartsWith("AC-30", StringComparison.Ordinal)).ToArray();
        string[] curingOnly = await NamesAsync(viewer, $"{ResourcesPath}?resourceTypeId={curingTypeId}");
        string[] activeBuilding = await NamesAsync(viewer, $"{ResourcesPath}?resourceTypeId={buildingTypeId}&isActive=true");
        string[] inactiveBuilding = await NamesAsync(viewer, $"{ResourcesPath}?resourceTypeId={buildingTypeId}&isActive=false");

        Assert.Equal(["AC-30 builder 0", "AC-30 builder 1", "AC-30 press 1", "AC-30 press 2"], all);
        Assert.Equal(["AC-30 press 1", "AC-30 press 2"], curingOnly);
        Assert.Equal(["AC-30 builder 1"], activeBuilding);
        Assert.Equal(["AC-30 builder 0"], inactiveBuilding);
        JsonElement listed = (await viewer.GetJsonAsync($"{ResourcesPath}?resourceTypeId={curingTypeId}"))[0];
        Assert.Equal("AC-30 B curing", listed.GetProperty("resourceType").GetProperty("name").GetString());
        Assert.Equal("Plant default", listed.GetProperty("calendar").GetProperty("name").GetString());
        Assert.True(listed.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task AC_24_AC_141_resource_name_and_resource_type_name_in_use_are_duplicates_without_regard_to_case()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-24 Type");
        await CreateResourceAsync(admin, "AC-24 Machine", typeId);
        int otherResourceId = await CreateResourceAsync(admin, "AC-24 other machine", typeId);

        HttpResponseMessage duplicateType = await admin.PostAsync(ResourceTypesPath, new { name = "ac-24 type" });
        HttpResponseMessage duplicateResource = await admin.PostAsync(ResourcesPath, new { name = "AC-24 MACHINE", resourceTypeId = typeId });
        HttpResponseMessage renamedToDuplicate = await admin.PutAsync($"{ResourcesPath}/{otherResourceId}", new { name = "AC-24 Machine", resourceTypeId = typeId, isActive = true });

        foreach (HttpResponseMessage response in new[] { duplicateType, duplicateResource, renamedToDuplicate })
        {
            JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.ConflictDuplicate);
            Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
        }
    }

    [Fact]
    public async Task AC_141_names_are_stored_without_surrounding_spaces()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        JsonElement resourceType = await ApiSession.ReadJsonAsync(await admin.PostAsync(ResourceTypesPath, new { name = "  AC-141 type " }));
        JsonElement resource = await ApiSession.ReadJsonAsync(
            await admin.PostAsync(ResourcesPath, new { name = " AC-141 machine  ", resourceTypeId = resourceType.GetProperty("id").GetInt32() }));

        Assert.Equal("AC-141 type", resourceType.GetProperty("name").GetString());
        Assert.Equal("AC-141 machine", resource.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AC_140_admin_renames_a_resource_type_and_deletes_unreferenced_items()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-140 type");
        int resourceId = await CreateResourceAsync(admin, "AC-140 machine", typeId);

        HttpResponseMessage renamed = await admin.PutAsync($"{ResourceTypesPath}/{typeId}", new { name = "AC-140 renamed type" });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("AC-140 renamed type", (await admin.GetJsonAsync($"{ResourcesPath}/{resourceId}")).GetProperty("resourceType").GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{ResourcesPath}/{resourceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{ResourceTypesPath}/{typeId}")).StatusCode);
        await ProblemAssert.HasProblemAsync(await admin.GetAsync($"{ResourcesPath}/{resourceId}"), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.DoesNotContain("AC-140 renamed type", await NamesAsync(admin, ResourceTypesPath));
        Assert.DoesNotContain("AC-140 machine", await NamesAsync(admin, ResourcesPath));
    }

    [Fact]
    public async Task AC_20_resource_type_with_a_machine_cannot_be_deleted_until_the_machine_is_gone()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-20 type");
        int resourceId = await CreateResourceAsync(admin, "AC-20 machine", typeId);

        HttpResponseMessage whileInUse = await admin.DeleteAsync($"{ResourceTypesPath}/{typeId}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(whileInUse, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Contains("AC-20 machine", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Contains("AC-20 type", await NamesAsync(admin, ResourceTypesPath));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{ResourcesPath}/{resourceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{ResourceTypesPath}/{typeId}")).StatusCode);
        Assert.DoesNotContain("AC-20 type", await NamesAsync(admin, ResourceTypesPath));
    }

    [Fact]
    public async Task AC_21_resource_without_name_and_type_reports_both_fields_and_an_unknown_type_its_field()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage emptyRequest = await admin.PostAsync(ResourcesPath, new { });
        HttpResponseMessage unknownType = await admin.PostAsync(ResourcesPath, new { name = "AC-21 machine", resourceTypeId = 987654 });

        JsonElement emptyProblem = await ProblemAssert.HasProblemAsync(emptyRequest, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["name", "resourceTypeId"], emptyProblem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order());
        JsonElement unknownTypeProblem = await ProblemAssert.HasProblemAsync(unknownType, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(unknownTypeProblem.GetProperty("errors").TryGetProperty("resourceTypeId", out _));
    }

    [Fact]
    public async Task AC_22_resource_with_a_wrong_value_type_is_request_malformed()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage response = await admin.PostAsync(ResourcesPath, new { name = "AC-22 machine", resourceTypeId = "one" });

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.RequestMalformed);
    }

    [Fact]
    public async Task AC_23_unknown_resource_or_resource_type_id_is_not_found()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-23 type");
        const int unknownId = 987654;

        HttpResponseMessage[] responses =
        [
            await admin.GetAsync($"{ResourcesPath}/{unknownId}"),
            await admin.PutAsync($"{ResourcesPath}/{unknownId}", new { name = "AC-23 machine", resourceTypeId = typeId, isActive = true }),
            await admin.DeleteAsync($"{ResourcesPath}/{unknownId}"),
            await admin.PutAsync($"{ResourceTypesPath}/{unknownId}", new { name = "AC-23 other type" }),
            await admin.DeleteAsync($"{ResourceTypesPath}/{unknownId}"),
        ];

        foreach (HttpResponseMessage response in responses)
        {
            await ProblemAssert.HasProblemAsync(response, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        }
    }

    [Theory]
    [InlineData(UserRole.Planner)]
    [InlineData(UserRole.Viewer)]
    public async Task AC_10_AC_12_viewer_and_planner_read_machines_but_cannot_write_them(UserRole role)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, $"AC-12 type for {role}");
        int resourceId = await CreateResourceAsync(admin, $"AC-12 machine for {role}", typeId);
        using ApiSession session = await api.Factory.SignInAsRoleAsync(role);

        HttpResponseMessage[] writes =
        [
            await session.PostAsync(ResourceTypesPath, new { name = "AC-12 refused type" }),
            await session.PutAsync($"{ResourceTypesPath}/{typeId}", new { name = "AC-12 refused rename" }),
            await session.DeleteAsync($"{ResourceTypesPath}/{typeId}"),
            await session.PostAsync(ResourcesPath, new { name = "AC-12 refused machine", resourceTypeId = typeId }),
            await session.PutAsync($"{ResourcesPath}/{resourceId}", new { name = "AC-12 refused rename", resourceTypeId = typeId, isActive = false }),
            await session.DeleteAsync($"{ResourcesPath}/{resourceId}"),
        ];

        foreach (HttpResponseMessage write in writes)
        {
            await ProblemAssert.HasProblemAsync(write, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        }

        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(ResourceTypesPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(ResourcesPath)).StatusCode);
        Assert.Equal($"AC-12 machine for {role}", (await session.GetJsonAsync($"{ResourcesPath}/{resourceId}")).GetProperty("name").GetString());
    }

    internal static async Task<int> CreateResourceTypeAsync(ApiSession admin, string name) =>
        (await ApiSession.ReadJsonAsync(await admin.PostAsync(ResourceTypesPath, new { name }))).GetProperty("id").GetInt32();

    internal static async Task<int> CreateResourceAsync(ApiSession admin, string name, int resourceTypeId) =>
        (await ApiSession.ReadJsonAsync(await admin.PostAsync(ResourcesPath, new { name, resourceTypeId }))).GetProperty("id").GetInt32();

    private static async Task<string[]> NamesAsync(ApiSession session, string path) =>
        (await session.GetJsonAsync(path)).EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray();
}

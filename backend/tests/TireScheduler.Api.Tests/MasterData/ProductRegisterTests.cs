using System.Net;
using System.Text.Json;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.MasterData;

// F-MD-2 through the API. The parts of AC-20, AC-32 and AC-33 that need orders or a routing are
// tested by the tasks that add them (M2-1, M2-3).
public sealed class ProductRegisterTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    private const string ProductsPath = "/api/v1/products";

    [Fact]
    public async Task AC_31_product_is_saved_with_trimmed_code_and_optional_spec()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage withSpec = await admin.PostAsync(ProductsPath, new { code = "  AC31-A ", name = "Passenger tire", spec = "205/55 R16" });
        HttpResponseMessage withoutSpec = await admin.PostAsync(ProductsPath, new { code = "AC31-B", name = "Truck tire" });

        Assert.Equal(HttpStatusCode.Created, withSpec.StatusCode);
        JsonElement saved = await admin.GetJsonAsync(withSpec.Headers.Location!.OriginalString);
        Assert.Equal("AC31-A", saved.GetProperty("code").GetString());
        Assert.Equal("Passenger tire", saved.GetProperty("name").GetString());
        Assert.Equal("205/55 R16", saved.GetProperty("spec").GetString());
        Assert.Equal(HttpStatusCode.Created, withoutSpec.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ApiSession.ReadJsonAsync(withoutSpec)).GetProperty("spec").ValueKind);
    }

    [Fact]
    public async Task AC_31_AC_24_product_code_is_unique_without_regard_to_case()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        await CreateProductAsync(admin, "PS-205", "First");
        int otherId = await CreateProductAsync(admin, "PS-206", "Other");

        HttpResponseMessage sameCodeInLowerCase = await admin.PostAsync(ProductsPath, new { code = "ps-205", name = "Second" });
        HttpResponseMessage changedToUsedCode = await admin.PutAsync($"{ProductsPath}/{otherId}", new { code = " Ps-205", name = "Other" });

        foreach (HttpResponseMessage response in new[] { sameCodeInLowerCase, changedToUsedCode })
        {
            JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.ConflictDuplicate);
            Assert.True(problem.GetProperty("errors").TryGetProperty("code", out _));
        }

        Assert.Equal("PS-206", (await admin.GetJsonAsync($"{ProductsPath}/{otherId}")).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AC_32_admin_changes_name_spec_and_code()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateProductAsync(admin, "AC32-OLD", "Old name");

        HttpResponseMessage response = await admin.PutAsync($"{ProductsPath}/{productId}", new { code = "AC32-NEW", name = "New name", spec = "New spec" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement saved = await admin.GetJsonAsync($"{ProductsPath}/{productId}");
        Assert.Equal("AC32-NEW", saved.GetProperty("code").GetString());
        Assert.Equal("New name", saved.GetProperty("name").GetString());
        Assert.Equal("New spec", saved.GetProperty("spec").GetString());
    }

    [Fact]
    public async Task AC_33_product_with_no_orders_is_deleted()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateProductAsync(admin, "AC33", "To delete");

        HttpResponseMessage response = await admin.DeleteAsync($"{ProductsPath}/{productId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await ProblemAssert.HasProblemAsync(await admin.GetAsync($"{ProductsPath}/{productId}"), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.DoesNotContain("AC33", (await admin.GetJsonAsync(ProductsPath)).EnumerateArray().Select(product => product.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AC_21_product_without_code_and_name_reports_both_fields()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage response = await admin.PostAsync(ProductsPath, new { code = "   ", spec = new string('x', 501) });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["code", "name", "spec"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order());
    }

    [Fact]
    public async Task AC_23_unknown_product_id_is_not_found()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        const int unknownId = 987654;

        HttpResponseMessage[] responses =
        [
            await admin.GetAsync($"{ProductsPath}/{unknownId}"),
            await admin.PutAsync($"{ProductsPath}/{unknownId}", new { code = "AC23", name = "Unknown" }),
            await admin.DeleteAsync($"{ProductsPath}/{unknownId}"),
        ];

        foreach (HttpResponseMessage response in responses)
        {
            await ProblemAssert.HasProblemAsync(response, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        }
    }

    [Theory]
    [InlineData(UserRole.Planner)]
    [InlineData(UserRole.Viewer)]
    public async Task AC_10_AC_12_viewer_and_planner_read_products_but_cannot_write_them(UserRole role)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateProductAsync(admin, $"AC12-{role}", "Kept");
        using ApiSession session = await api.Factory.SignInAsRoleAsync(role);

        HttpResponseMessage[] writes =
        [
            await session.PostAsync(ProductsPath, new { code = "AC12-REFUSED", name = "Refused" }),
            await session.PutAsync($"{ProductsPath}/{productId}", new { code = $"AC12-{role}", name = "Refused rename" }),
            await session.DeleteAsync($"{ProductsPath}/{productId}"),
        ];

        foreach (HttpResponseMessage write in writes)
        {
            await ProblemAssert.HasProblemAsync(write, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        }

        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(ProductsPath)).StatusCode);
        Assert.Equal("Kept", (await session.GetJsonAsync($"{ProductsPath}/{productId}")).GetProperty("name").GetString());
    }

    private static async Task<int> CreateProductAsync(ApiSession admin, string code, string name) =>
        (await ApiSession.ReadJsonAsync(await admin.PostAsync(ProductsPath, new { code, name }))).GetProperty("id").GetInt32();
}

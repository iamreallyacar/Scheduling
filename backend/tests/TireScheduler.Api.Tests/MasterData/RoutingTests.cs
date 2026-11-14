using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.MasterData;

// F-MD-3 through the API, with the parts of AC-20 and AC-33 that need a routing.
public sealed class RoutingTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    [Fact]
    public async Task AC_34_routing_is_saved_as_a_whole_with_defaults_for_setup_units_and_lag()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int buildingTypeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-34 building");
        int curingTypeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-34 curing");
        int productId = await CreateProductAsync(admin, "AC34");

        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new
        {
            stages = new object[]
            {
                new { sequenceNo = 2, resourceTypeId = curingTypeId, setupMinutes = 30, cycleMinutes = 12, unitsPerCycle = 4 },
                new { sequenceNo = 1, resourceTypeId = buildingTypeId, cycleMinutes = 5 },
            },
            dependencies = new object[] { new { predecessorSequenceNo = 1, successorSequenceNo = 2 } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement saved = await admin.GetJsonAsync(RoutingPath(productId));
        Assert.Equal(saved.GetRawText(), (await ApiSession.ReadJsonAsync(response)).GetRawText());
        JsonElement[] stages = saved.GetProperty("stages").EnumerateArray().ToArray();
        Assert.Equal([(1, buildingTypeId, 0, 5, 1), (2, curingTypeId, 30, 12, 4)], stages.Select(StageValues));
        Assert.Equal("AC-34 building", stages[0].GetProperty("resourceType").GetProperty("name").GetString());
        Assert.Equal([(1, 2, 0)], saved.GetProperty("dependencies").EnumerateArray().Select(DependencyValues));
    }

    [Fact]
    public async Task AC_34_saving_again_replaces_the_previous_stages_and_dependencies()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-34 replace");
        int productId = await CreateProductAsync(admin, "AC34-REPLACE");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId), Stage(2, typeId), Stage(3, typeId)], [Dependency(1, 2), Dependency(2, 3, minLagMinutes: 60)]);

        // Stages 1 and 2 may both run first; stage 3 waits for both (parallel branches).
        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new
        {
            stages = new[] { Stage(1, typeId, cycleMinutes: 7), Stage(2, typeId), Stage(3, typeId) },
            dependencies = new[] { Dependency(1, 3, minLagMinutes: 15), Dependency(2, 3) },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement saved = await admin.GetJsonAsync(RoutingPath(productId));
        Assert.Equal([7, 10, 10], saved.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("cycleMinutes").GetInt32()));
        Assert.Equal([(1, 3, 15), (2, 3, 0)], saved.GetProperty("dependencies").EnumerateArray().Select(DependencyValues));
    }

    [Fact]
    public async Task AC_142_every_role_reads_a_routing_and_a_product_without_one_has_empty_lists()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-142 type");
        int routedProductId = await CreateProductAsync(admin, "AC142-ROUTED");
        int newProductId = await CreateProductAsync(admin, "AC142-NEW");
        int clearedProductId = await CreateProductAsync(admin, "AC142-CLEARED");
        await SaveRoutingAsync(admin, routedProductId, [Stage(1, typeId)], []);
        await SaveRoutingAsync(admin, clearedProductId, [Stage(1, typeId), Stage(2, typeId)], [Dependency(1, 2)]);

        await SaveRoutingAsync(admin, clearedProductId, [], []);

        foreach (UserRole role in new[] { UserRole.Viewer, UserRole.Planner })
        {
            using ApiSession reader = await api.Factory.SignInAsRoleAsync(role);
            Assert.Equal(1, (await reader.GetJsonAsync(RoutingPath(routedProductId))).GetProperty("stages").GetArrayLength());
            foreach (int productWithoutRouting in new[] { newProductId, clearedProductId })
            {
                JsonElement routing = await reader.GetJsonAsync(RoutingPath(productWithoutRouting));
                Assert.Equal(0, routing.GetProperty("stages").GetArrayLength());
                Assert.Equal(0, routing.GetProperty("dependencies").GetArrayLength());
            }
        }
    }

    [Theory]
    [InlineData(new[] { 1, 3 })]
    [InlineData(new[] { 1, 2, 2 })]
    [InlineData(new[] { 2, 3 })]
    [InlineData(new[] { 0, 1 })]
    public async Task AC_35_sequence_numbers_with_gaps_or_repeats_are_rejected(int[] sequenceNumbers)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, $"AC-35 type {string.Join('-', sequenceNumbers)}");
        int productId = await CreateProductAsync(admin, $"AC35-{string.Join('-', sequenceNumbers)}");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId)], []);

        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new
        {
            stages = sequenceNumbers.Select(sequenceNo => Stage(sequenceNo, typeId)),
            dependencies = Array.Empty<object>(),
        });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(problem.GetProperty("errors").TryGetProperty("stages", out _));
        Assert.Equal(1, (await admin.GetJsonAsync(RoutingPath(productId))).GetProperty("stages").GetArrayLength());
    }

    [Theory]
    [InlineData("1>2,2>3,3>1", "1, 2, 3")]
    [InlineData("1>2,2>1", "1, 2")]
    [InlineData("2>2", "Stage 2")]
    [InlineData("1>2,1>2", "stage 1 to stage 2")]
    [InlineData("1>2:-1", "stage 1 to stage 2")]
    [InlineData("1>4", "stage 1 to stage 4")]
    public async Task AC_36_invalid_dependencies_are_rejected_naming_the_stages(string dependencyList, string expectedStageNames)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, $"AC-36 type {dependencyList}");
        int productId = await CreateProductAsync(admin, $"AC36-{dependencyList}");
        object[] dependencies = dependencyList.Split(',').Select(ParseDependency).ToArray();

        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new
        {
            stages = new[] { Stage(1, typeId), Stage(2, typeId), Stage(3, typeId) },
            dependencies,
        });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.RoutingInvalidDependency);
        Assert.Contains(expectedStageNames, problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, (await admin.GetJsonAsync(RoutingPath(productId))).GetProperty("stages").GetArrayLength());
    }

    [Fact]
    public async Task AC_37_unknown_resource_type_is_rejected_on_the_field_of_its_stage()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-37 type");
        int productId = await CreateProductAsync(admin, "AC37");
        const int unknownTypeId = 987654;

        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new
        {
            stages = new[] { Stage(1, typeId), Stage(2, unknownTypeId) },
            dependencies = Array.Empty<object>(),
        });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["stages[1].resourceTypeId"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
        Assert.Equal(0, (await admin.GetJsonAsync(RoutingPath(productId))).GetProperty("stages").GetArrayLength());
    }

    [Fact]
    public async Task AC_34_AC_21_missing_and_out_of_range_stage_values_are_reported_by_field_path()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-21 routing type");
        int productId = await CreateProductAsync(admin, "AC21-ROUTING");

        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new
        {
            stages = new object[]
            {
                new { sequenceNo = 1, resourceTypeId = typeId, setupMinutes = -1, cycleMinutes = 0, unitsPerCycle = 0 },
                new { sequenceNo = 2 },
            },
            dependencies = new object[] { new { successorSequenceNo = 2 } },
        });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(
            [
                "dependencies[0].predecessorSequenceNo",
                "stages[0].cycleMinutes",
                "stages[0].setupMinutes",
                "stages[0].unitsPerCycle",
                "stages[1].cycleMinutes",
                "stages[1].resourceTypeId",
            ],
            problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AC_21_routing_without_its_lists_is_rejected()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateProductAsync(admin, "AC21-NO-LISTS");

        HttpResponseMessage response = await admin.PutAsync(RoutingPath(productId), new { });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["dependencies", "stages"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order());
    }

    [Fact]
    public async Task AC_20_resource_type_used_by_a_routing_stage_cannot_be_deleted()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-20 routed type");
        int productId = await CreateProductAsync(admin, "AC20-ROUTED");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId)], []);

        HttpResponseMessage whileUsed = await admin.DeleteAsync($"/api/v1/resource-types/{typeId}");
        await SaveRoutingAsync(admin, productId, [], []);
        HttpResponseMessage afterRoutingIsGone = await admin.DeleteAsync($"/api/v1/resource-types/{typeId}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(whileUsed, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Contains("AC20-ROUTED", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, afterRoutingIsGone.StatusCode);
    }

    [Fact]
    public async Task AC_33_product_is_deleted_together_with_its_routing()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "AC-33 type");
        int productId = await CreateProductAsync(admin, "AC33-ROUTED");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId), Stage(2, typeId)], [Dependency(1, 2)]);

        HttpResponseMessage response = await admin.DeleteAsync($"/api/v1/products/{productId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await ProblemAssert.HasProblemAsync(await admin.GetAsync(RoutingPath(productId)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        MasterDataDbContext context = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        Assert.False(await context.RoutingStages.AnyAsync(stage => stage.ProductId == productId, TestContext.Current.CancellationToken));
        Assert.False(await context.RoutingDependencies.AnyAsync(dependency => dependency.ProductId == productId, TestContext.Current.CancellationToken));
        // The type is free again once the routing is gone.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/resource-types/{typeId}")).StatusCode);
    }

    [Fact]
    public async Task AC_23_routing_of_an_unknown_product_is_not_found()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        const int unknownProductId = 987654;

        HttpResponseMessage read = await admin.GetAsync(RoutingPath(unknownProductId));
        HttpResponseMessage write = await admin.PutAsync(RoutingPath(unknownProductId), new { stages = Array.Empty<object>(), dependencies = Array.Empty<object>() });

        await ProblemAssert.HasProblemAsync(read, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(write, HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Theory]
    [InlineData(UserRole.Planner)]
    [InlineData(UserRole.Viewer)]
    public async Task AC_10_AC_12_viewer_and_planner_cannot_save_a_routing(UserRole role)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateProductAsync(admin, $"AC12-ROUTING-{role}");
        using ApiSession session = await api.Factory.SignInAsRoleAsync(role);

        HttpResponseMessage response = await session.PutAsync(RoutingPath(productId), new { stages = Array.Empty<object>(), dependencies = Array.Empty<object>() });

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
    }

    private static string RoutingPath(int productId) => $"/api/v1/products/{productId}/routing";

    private static object Stage(int sequenceNo, int resourceTypeId, int cycleMinutes = 10) => new { sequenceNo, resourceTypeId, cycleMinutes };

    private static object Dependency(int predecessorSequenceNo, int successorSequenceNo, int minLagMinutes = 0) =>
        new { predecessorSequenceNo, successorSequenceNo, minLagMinutes };

    // "1>2" or, with a lag, "1>2:-1".
    private static object ParseDependency(string text)
    {
        string[] stagesAndLag = text.Split(':');
        string[] stages = stagesAndLag[0].Split('>');
        int lag = stagesAndLag.Length > 1 ? int.Parse(stagesAndLag[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
        return Dependency(
            int.Parse(stages[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(stages[1], System.Globalization.CultureInfo.InvariantCulture),
            lag);
    }

    private static (int SequenceNo, int ResourceTypeId, int SetupMinutes, int CycleMinutes, int UnitsPerCycle) StageValues(JsonElement stage) =>
        (stage.GetProperty("sequenceNo").GetInt32(),
            stage.GetProperty("resourceType").GetProperty("id").GetInt32(),
            stage.GetProperty("setupMinutes").GetInt32(),
            stage.GetProperty("cycleMinutes").GetInt32(),
            stage.GetProperty("unitsPerCycle").GetInt32());

    private static (int Predecessor, int Successor, int MinLagMinutes) DependencyValues(JsonElement dependency) =>
        (dependency.GetProperty("predecessorSequenceNo").GetInt32(),
            dependency.GetProperty("successorSequenceNo").GetInt32(),
            dependency.GetProperty("minLagMinutes").GetInt32());

    private static async Task<int> CreateProductAsync(ApiSession admin, string code) =>
        (await ApiSession.ReadJsonAsync(await admin.PostAsync("/api/v1/products", new { code, name = $"Product {code}" }))).GetProperty("id").GetInt32();

    private static async Task SaveRoutingAsync(ApiSession admin, int productId, object[] stages, object[] dependencies) =>
        await ApiSession.ReadJsonAsync(await admin.PutAsync(RoutingPath(productId), new { stages, dependencies }));
}

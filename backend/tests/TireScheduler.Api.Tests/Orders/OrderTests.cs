using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Security;
using TireScheduler.Api.Tests.MasterData;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.Orders;

// F-ORD-1 and F-ORD-3 through the API, with the F-NFR-1 and F-NFR-2 cases that need orders.
public sealed class OrderTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    [Fact]
    public async Task AC_45_AC_11_AC_147_planner_creates_an_order_with_trimmed_text_and_priority_normal_by_default()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC45");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);

        HttpResponseMessage response = await planner.PostAsync(OrdersPath, NewOrder(" SO-45 ", productId, quantity: 40, dueDate: "2026-12-15", customerName: "  Tire Shop South "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement answer = await ApiSession.ReadJsonAsync(response);
        Assert.Equal(["id"], answer.EnumerateObject().Select(field => field.Name));
        int orderId = answer.GetProperty("id").GetInt32();
        Assert.Equal(OrderPath(orderId), response.Headers.Location?.OriginalString);
        JsonElement order = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal("SO-45", order.GetProperty("orderNo").GetString());
        Assert.Equal("Tire Shop South", order.GetProperty("customerName").GetString());
        Assert.Equal(productId, order.GetProperty("product").GetProperty("id").GetInt32());
        Assert.Equal("AC45", order.GetProperty("product").GetProperty("code").GetString());
        Assert.Equal(40, order.GetProperty("quantity").GetInt32());
        Assert.Equal("2026-12-15", order.GetProperty("dueDate").GetString());
        Assert.Equal("Normal", order.GetProperty("priority").GetString());
        Assert.Equal(JsonValueKind.Null, order.GetProperty("cancelledAt").ValueKind);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("High")]
    public async Task AC_45_priority_low_and_high_are_saved(string priority)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, $"AC45-{priority}");

        JsonElement order = await CreateOrderAsync(admin, NewOrder($"SO-45-{priority}", productId, priority: priority));

        Assert.Equal(priority, order.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task AC_45_AC_24_order_number_is_unique_without_regard_to_case()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC45-UNIQUE");
        await CreateOrderAsync(admin, NewOrder("SO-UNIQUE", productId));

        HttpResponseMessage response = await admin.PostAsync(OrdersPath, NewOrder("so-unique", productId));

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.ConflictDuplicate);
        Assert.True(problem.GetProperty("errors").TryGetProperty("orderNo", out _));
    }

    [Fact]
    public async Task AC_45_AC_21_missing_fields_and_a_quantity_below_1_are_reported_together()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage empty = await admin.PostAsync(OrdersPath, new { });
        HttpResponseMessage zeroQuantity = await admin.PostAsync(OrdersPath, NewOrder("SO-ZERO", productId: 1, quantity: 0));

        JsonElement emptyProblem = await ProblemAssert.HasProblemAsync(empty, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(
            ["customerName", "dueDate", "orderNo", "productId", "quantity"],
            ProblemAssert.ErrorFields(emptyProblem));
        JsonElement quantityProblem = await ProblemAssert.HasProblemAsync(zeroQuantity, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["quantity"], ProblemAssert.ErrorFields(quantityProblem));
    }

    [Fact]
    public async Task AC_45_AC_22_an_unknown_priority_is_rejected()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC45-PRIORITY");

        HttpResponseMessage response = await admin.PostAsync(OrdersPath, NewOrder("SO-URGENT", productId, priority: "Urgent"));

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.RequestMalformed);
    }

    [Fact]
    public async Task AC_46_a_product_without_a_routing_or_an_unknown_product_is_rejected_on_the_product_field()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productWithoutRoutingId = await CreateProductAsync(admin, "AC46");
        const int unknownProductId = 987654;

        HttpResponseMessage withoutRouting = await admin.PostAsync(OrdersPath, NewOrder("SO-46", productWithoutRoutingId));
        HttpResponseMessage unknownProduct = await admin.PostAsync(OrdersPath, NewOrder("SO-46-UNKNOWN", unknownProductId));

        foreach (HttpResponseMessage response in new[] { withoutRouting, unknownProduct })
        {
            JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
            Assert.Equal(["productId"], ProblemAssert.ErrorFields(problem));
        }
    }

    [Fact]
    public async Task AC_61_AC_62_AC_63_AC_146_operations_dependencies_and_one_lot_per_operation_are_generated_from_the_routing()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int extrusionTypeId = await CreateResourceTypeAsync(admin, "AC-61 extrusion");
        int buildingTypeId = await CreateResourceTypeAsync(admin, "AC-61 building");
        int curingTypeId = await CreateResourceTypeAsync(admin, "AC-61 curing");
        int inspectionTypeId = await CreateResourceTypeAsync(admin, "AC-61 inspection");
        int productId = await CreateProductAsync(admin, "AC61");
        await SaveRoutingAsync(
            admin,
            productId,
            [
                Stage(1, extrusionTypeId, cycleMinutes: 2),
                Stage(2, buildingTypeId, cycleMinutes: 5, setupMinutes: 15),
                Stage(3, curingTypeId, cycleMinutes: 12, setupMinutes: 30, unitsPerCycle: 2),
                Stage(4, inspectionTypeId, cycleMinutes: 1),
            ],
            [Dependency(1, 2), Dependency(2, 3, minLagMinutes: 120), Dependency(3, 4)]);
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);

        JsonElement created = await CreateOrderAsync(planner, NewOrder("SO-61", productId, quantity: 101));

        JsonElement[] operations = created.GetProperty("operations").EnumerateArray().ToArray();
        Assert.Equal(
            [(1, extrusionTypeId, 101, 0, 2, 1), (2, buildingTypeId, 101, 15, 5, 1), (3, curingTypeId, 101, 30, 12, 2), (4, inspectionTypeId, 101, 0, 1, 1)],
            operations.Select(OperationValues));
        Assert.Equal("AC-61 curing", operations[2].GetProperty("resourceType").GetProperty("name").GetString());
        Assert.Equal([(1, 2, 0), (2, 3, 120), (3, 4, 0)], created.GetProperty("dependencies").EnumerateArray().Select(DependencyValues));
        // 101 x 2, 15 + 101 x 5, 30 + ceil(101 / 2) x 12 = 642 (AC-63), 101 x 1.
        Assert.Equal(
            [[(1, 101, 202)], [(1, 101, 520)], [(1, 101, 642)], [(1, 101, 101)]],
            operations.Select(operation => operation.GetProperty("lots").EnumerateArray().Select(LotValues).ToArray()));
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);
        Assert.Equal(created.GetRawText(), (await viewer.GetJsonAsync(OrderPath(created.GetProperty("id").GetInt32()))).GetRawText());
    }

    [Fact]
    public async Task AC_145_a_lot_of_one_year_is_saved_and_a_longer_one_is_rejected_on_the_quantity_naming_the_stage()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-145 type");
        int productId = await CreateProductAsync(admin, "AC145");
        // 525,599 + 1 x quantity: one year (525,600 minutes) for 1 tire, a minute more for 2.
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId), Stage(2, typeId, cycleMinutes: 1, setupMinutes: 525_599)], [Dependency(1, 2)]);
        JsonElement order = await CreateOrderAsync(admin, NewOrder("SO-145", productId, quantity: 1));
        Assert.Equal(525_600, order.GetProperty("operations")[1].GetProperty("lots")[0].GetProperty("durationMinutes").GetInt32());

        HttpResponseMessage created = await admin.PostAsync(OrdersPath, NewOrder("SO-145-B", productId, quantity: 2));
        HttpResponseMessage changed = await admin.PutAsync(OrderPath(order.GetProperty("id").GetInt32()), NewOrder("SO-145", productId, quantity: 2));

        foreach (HttpResponseMessage response in new[] { created, changed })
        {
            JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
            string message = Assert.Single(problem.GetProperty("errors").GetProperty("quantity").EnumerateArray()).GetString()!;
            Assert.Contains("stage 2", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AC_47_AC_147_planner_changes_order_number_customer_due_date_and_priority_and_operations_stay()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC47");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        JsonElement order = await CreateOrderAsync(planner, NewOrder("SO-47", productId, quantity: 20));
        int orderId = order.GetProperty("id").GetInt32();

        HttpResponseMessage response = await planner.PutAsync(
            OrderPath(orderId),
            NewOrder("SO-47-B", productId, quantity: 20, dueDate: "2027-01-31", priority: "High", customerName: "Fleet Service"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement saved = await planner.GetJsonAsync(OrderPath(orderId));
        Assert.Equal(("SO-47-B", "Fleet Service", "2027-01-31", "High"), (
            saved.GetProperty("orderNo").GetString(),
            saved.GetProperty("customerName").GetString(),
            saved.GetProperty("dueDate").GetString(),
            saved.GetProperty("priority").GetString()));
        Assert.Equal(order.GetProperty("operations").GetRawText(), saved.GetProperty("operations").GetRawText());
        Assert.Equal(order.GetProperty("createdAt").GetString(), saved.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task AC_48_changing_the_quantity_generates_operations_and_lots_again_from_the_current_routing()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-48 type");
        int productId = await CreateProductAsync(admin, "AC48");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId, cycleMinutes: 5)], []);
        JsonElement order = await CreateOrderAsync(admin, NewOrder("SO-48", productId, quantity: 10));
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId, cycleMinutes: 3), Stage(2, typeId, cycleMinutes: 4)], [Dependency(1, 2, minLagMinutes: 30)]);

        int orderId = order.GetProperty("id").GetInt32();

        HttpResponseMessage response = await admin.PutAsync(OrderPath(orderId), NewOrder("SO-48", productId, quantity: 12));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement saved = await admin.GetJsonAsync(OrderPath(orderId));
        Assert.Equal(12, saved.GetProperty("quantity").GetInt32());
        Assert.Equal([(1, typeId, 12, 0, 3, 1), (2, typeId, 12, 0, 4, 1)], saved.GetProperty("operations").EnumerateArray().Select(OperationValues));
        Assert.Equal([(1, 2, 30)], saved.GetProperty("dependencies").EnumerateArray().Select(DependencyValues));
        Assert.Equal([36, 48], saved.GetProperty("operations").EnumerateArray().Select(operation => operation.GetProperty("lots")[0].GetProperty("durationMinutes").GetInt32()));
        Assert.Empty(LotIds(saved).Intersect(LotIds(order)));
    }

    [Fact]
    public async Task AC_48_changing_the_product_generates_operations_from_the_new_products_routing()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int firstProductId = await CreateRoutedProductAsync(admin, "AC48-FIRST");
        int secondTypeId = await CreateResourceTypeAsync(admin, "AC-48 second type");
        int secondProductId = await CreateProductAsync(admin, "AC48-SECOND");
        await SaveRoutingAsync(admin, secondProductId, [Stage(1, secondTypeId, cycleMinutes: 7)], []);
        JsonElement order = await CreateOrderAsync(admin, NewOrder("SO-48-PRODUCT", firstProductId, quantity: 3));

        int orderId = order.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync(OrderPath(orderId), NewOrder("SO-48-PRODUCT", secondProductId, quantity: 3))).StatusCode);

        JsonElement saved = await admin.GetJsonAsync(OrderPath(orderId));

        Assert.Equal("AC48-SECOND", saved.GetProperty("product").GetProperty("code").GetString());
        Assert.Equal([(1, secondTypeId, 3, 0, 7, 1)], saved.GetProperty("operations").EnumerateArray().Select(OperationValues));
        Assert.Equal(0, saved.GetProperty("dependencies").GetArrayLength());
    }

    [Fact]
    public async Task AC_46_changing_to_a_product_without_a_routing_is_rejected_and_the_order_is_unchanged()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int routedProductId = await CreateRoutedProductAsync(admin, "AC46-ROUTED");
        int productWithoutRoutingId = await CreateProductAsync(admin, "AC46-NONE");
        JsonElement order = await CreateOrderAsync(admin, NewOrder("SO-46-EDIT", routedProductId));
        int orderId = order.GetProperty("id").GetInt32();

        HttpResponseMessage response = await admin.PutAsync(OrderPath(orderId), NewOrder("SO-46-EDIT", productWithoutRoutingId));

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.True(problem.GetProperty("errors").TryGetProperty("productId", out _));
        Assert.Equal(order.GetRawText(), (await admin.GetJsonAsync(OrderPath(orderId))).GetRawText());
    }

    [Fact]
    public async Task AC_39_changing_the_routing_leaves_existing_orders_unchanged()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-39 type");
        int productId = await CreateProductAsync(admin, "AC39");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId, cycleMinutes: 5), Stage(2, typeId, cycleMinutes: 6)], [Dependency(1, 2, minLagMinutes: 10)]);
        JsonElement order = await CreateOrderAsync(admin, NewOrder("SO-39", productId));

        await SaveRoutingAsync(admin, productId, [Stage(1, typeId, cycleMinutes: 9)], []);

        Assert.Equal(order.GetRawText(), (await admin.GetJsonAsync(OrderPath(order.GetProperty("id").GetInt32()))).GetRawText());
    }

    [Fact]
    public async Task AC_32_an_order_keeps_pointing_to_its_product_after_the_product_is_changed()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC32");
        JsonElement order = await CreateOrderAsync(admin, NewOrder("SO-32", productId));

        HttpResponseMessage response = await admin.PutAsync($"/api/v1/products/{productId}", new { code = "AC32-NEW", name = "Renamed tire", spec = "205/55 R16" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement product = (await admin.GetJsonAsync(OrderPath(order.GetProperty("id").GetInt32()))).GetProperty("product");
        Assert.Equal((productId, "AC32-NEW", "Renamed tire"), (product.GetProperty("id").GetInt32(), product.GetProperty("code").GetString(), product.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task AC_20_a_product_used_by_an_order_cannot_be_deleted_and_the_detail_names_the_order()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC20-ORDERED");
        await CreateOrderAsync(admin, NewOrder("SO-20", productId));

        HttpResponseMessage response = await admin.DeleteAsync($"/api/v1/products/{productId}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Contains("SO-20", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/products/{productId}")).StatusCode);
    }

    [Fact]
    public async Task AC_20_a_resource_type_used_by_an_operation_cannot_be_deleted_after_the_routing_no_longer_uses_it()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int typeId = await CreateResourceTypeAsync(admin, "AC-20 operation type");
        int productId = await CreateProductAsync(admin, "AC20-OPERATION");
        await SaveRoutingAsync(admin, productId, [Stage(1, typeId)], []);
        await CreateOrderAsync(admin, NewOrder("SO-20-OPERATION", productId));
        await SaveRoutingAsync(admin, productId, [], []);

        HttpResponseMessage response = await admin.DeleteAsync($"/api/v1/resource-types/{typeId}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.ConflictInUse);
        Assert.Contains("SO-20-OPERATION", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC_50_AC_147_cancelling_sets_the_cancellation_time_and_AC_52_a_cancelled_order_cannot_be_edited_or_cancelled_again()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC50");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int orderId = (await CreateOrderAsync(planner, NewOrder("SO-50", productId))).GetProperty("id").GetInt32();

        HttpResponseMessage cancelled = await planner.PostAsync($"{OrderPath(orderId)}/cancel");
        HttpResponseMessage cancelledAgain = await planner.PostAsync($"{OrderPath(orderId)}/cancel");
        HttpResponseMessage edited = await planner.PutAsync(OrderPath(orderId), NewOrder("SO-50", productId, customerName: "Someone else"));

        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Empty(await cancelled.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(JsonValueKind.Null, (await planner.GetJsonAsync(OrderPath(orderId))).GetProperty("cancelledAt").ValueKind);
        await ProblemAssert.HasProblemAsync(cancelledAgain, HttpStatusCode.Conflict, ProblemCodes.OrderCancelled);
        await ProblemAssert.HasProblemAsync(edited, HttpStatusCode.Conflict, ProblemCodes.OrderCancelled);
        Assert.Equal("Tire Shop North", (await planner.GetJsonAsync(OrderPath(orderId))).GetProperty("customerName").GetString());
    }

    [Fact]
    public async Task AC_18_a_pending_order_is_deleted_softly_and_is_then_missing()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC18");
        using ApiSession planner = await api.Factory.SignInAsRoleAsync(UserRole.Planner);
        int orderId = (await CreateOrderAsync(planner, NewOrder("SO-18", productId))).GetProperty("id").GetInt32();

        HttpResponseMessage response = await planner.DeleteAsync(OrderPath(orderId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await ProblemAssert.HasProblemAsync(await planner.GetAsync(OrderPath(orderId)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        Assert.DoesNotContain(orderId, await ListedOrderIdsAsync(planner));
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        OrdersDbContext context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        Order row = await context.Orders.IgnoreQueryFilters().SingleAsync(order => order.Id == orderId, TestContext.Current.CancellationToken);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task AC_19_a_cancelled_order_cannot_be_deleted_and_the_detail_says_to_cancel_instead()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC19");
        int orderId = (await CreateOrderAsync(admin, NewOrder("SO-19", productId))).GetProperty("id").GetInt32();
        await CancelOrderAsync(admin, orderId);

        HttpResponseMessage response = await admin.DeleteAsync(OrderPath(orderId));

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.OrderNotDeletable);
        Assert.Contains("cancel", problem.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(OrderPath(orderId))).StatusCode);
    }

    [Fact]
    public async Task A25_a_deleted_order_frees_its_order_number()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "A25");
        int deletedOrderId = (await CreateOrderAsync(admin, NewOrder("SO-A25", productId))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(OrderPath(deletedOrderId))).StatusCode);

        HttpResponseMessage response = await admin.PostAsync(OrdersPath, NewOrder("so-a25", productId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AC_23_an_unknown_or_deleted_order_is_not_found()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC23-ORDER");
        int deletedOrderId = (await CreateOrderAsync(admin, NewOrder("SO-23", productId))).GetProperty("id").GetInt32();
        await admin.DeleteAsync(OrderPath(deletedOrderId));
        const int unknownOrderId = 987654;

        foreach (int orderId in new[] { unknownOrderId, deletedOrderId })
        {
            await ProblemAssert.HasProblemAsync(await admin.GetAsync(OrderPath(orderId)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
            await ProblemAssert.HasProblemAsync(await admin.PutAsync(OrderPath(orderId), NewOrder("SO-23", productId)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
            await ProblemAssert.HasProblemAsync(await admin.PostAsync($"{OrderPath(orderId)}/cancel"), HttpStatusCode.NotFound, ProblemCodes.NotFound);
            await ProblemAssert.HasProblemAsync(await admin.DeleteAsync(OrderPath(orderId)), HttpStatusCode.NotFound, ProblemCodes.NotFound);
        }
    }

    [Fact]
    public async Task AC_10_a_viewer_reads_orders_but_cannot_write_them()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC10-ORDER");
        int orderId = (await CreateOrderAsync(admin, NewOrder("SO-10", productId))).GetProperty("id").GetInt32();
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(OrderPath(orderId))).StatusCode);
        Assert.Contains(orderId, await ListedOrderIdsAsync(viewer));
        await ProblemAssert.HasProblemAsync(await viewer.PostAsync(OrdersPath, NewOrder("SO-10-V", productId)), HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        await ProblemAssert.HasProblemAsync(await viewer.PutAsync(OrderPath(orderId), NewOrder("SO-10", productId)), HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        await ProblemAssert.HasProblemAsync(await viewer.PostAsync($"{OrderPath(orderId)}/cancel"), HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        await ProblemAssert.HasProblemAsync(await viewer.DeleteAsync(OrderPath(orderId)), HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
    }

    private static async Task<int[]> ListedOrderIdsAsync(ApiSession session) =>
        (await session.GetJsonAsync($"{OrdersPath}?pageSize=200")).GetProperty("items").EnumerateArray().Select(order => order.GetProperty("id").GetInt32()).ToArray();

    private static (int SequenceNo, int ResourceTypeId, int Quantity, int SetupMinutes, int CycleMinutes, int UnitsPerCycle) OperationValues(JsonElement operation) =>
        (operation.GetProperty("sequenceNo").GetInt32(),
            operation.GetProperty("resourceType").GetProperty("id").GetInt32(),
            operation.GetProperty("quantity").GetInt32(),
            operation.GetProperty("setupMinutes").GetInt32(),
            operation.GetProperty("cycleMinutes").GetInt32(),
            operation.GetProperty("unitsPerCycle").GetInt32());

    private static (int Predecessor, int Successor, int MinLagMinutes) DependencyValues(JsonElement dependency) =>
        (dependency.GetProperty("predecessorSequenceNo").GetInt32(),
            dependency.GetProperty("successorSequenceNo").GetInt32(),
            dependency.GetProperty("minLagMinutes").GetInt32());

    private static (int LotNo, int Quantity, int DurationMinutes) LotValues(JsonElement lot) =>
        (lot.GetProperty("lotNo").GetInt32(), lot.GetProperty("quantity").GetInt32(), lot.GetProperty("durationMinutes").GetInt32());
}

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;
using static TireScheduler.Api.Tests.TestData.OrderTestData;

namespace TireScheduler.Api.Tests.Orders;

// F-ORD-4: paging, search, filters and sorts of the order list. One database per test, so the
// counts are exact.
public sealed class OrderListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_65_without_paging_values_the_list_is_page_1_with_50_orders_and_the_total_count()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC65");
        for (int number = 1; number <= 55; number++)
        {
            await CreateOrderAsync(admin, NewOrder($"SO-{number:000}", productId));
        }

        JsonElement firstPage = await admin.GetJsonAsync(OrdersPath);
        JsonElement secondPage = await admin.GetJsonAsync($"{OrdersPath}?page=2");

        Assert.Equal((1, 50, 55), (firstPage.GetProperty("page").GetInt32(), firstPage.GetProperty("pageSize").GetInt32(), firstPage.GetProperty("totalCount").GetInt32()));
        Assert.Equal(50, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(5, secondPage.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("pageSize=201", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task AC_65_a_page_size_above_200_or_a_page_below_1_is_rejected(string query, string expectedField)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage response = await viewer.GetAsync($"{OrdersPath}?{query}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal([expectedField], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
    }

    [Fact]
    public async Task AC_68_AC_69_by_default_orders_are_sorted_by_due_date_then_order_number_and_deleted_orders_are_never_listed()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC68");
        await CreateOrderAsync(admin, NewOrder("SO-B", productId, dueDate: "2026-12-02"));
        await CreateOrderAsync(admin, NewOrder("SO-C", productId, dueDate: "2026-12-01"));
        await CreateOrderAsync(admin, NewOrder("SO-A", productId, dueDate: "2026-12-02"));
        int deletedOrderId = (await CreateOrderAsync(admin, NewOrder("SO-0", productId, dueDate: "2026-11-30"))).GetProperty("id").GetInt32();
        await admin.DeleteAsync(OrderPath(deletedOrderId));

        JsonElement page = await admin.GetJsonAsync(OrdersPath);

        Assert.Equal(["SO-C", "SO-A", "SO-B"], page.GetProperty("items").EnumerateArray().Select(order => order.GetProperty("orderNo").GetString()));
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        JsonElement first = page.GetProperty("items")[0];
        Assert.Equal(("AC68", 10, "Normal"), (first.GetProperty("product").GetProperty("code").GetString(), first.GetProperty("quantity").GetInt32(), first.GetProperty("priority").GetString()));
    }

    [Fact]
    public async Task AC_66_a_search_text_finds_orders_by_order_number_customer_product_code_or_product_name_without_regard_to_case()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int searchProductId = await CreateRoutedProductAsync(admin, "SRCH-A");
        int zetaProductId = await CreateRoutedProductAsync(admin, "ZETA");
        await CreateOrderAsync(admin, NewOrder("AB-100", searchProductId, customerName: "Alpha Tires"));
        await CreateOrderAsync(admin, NewOrder("AB-200", zetaProductId, customerName: "Beta Wheels"));
        await CreateOrderAsync(admin, NewOrder("XY-300", zetaProductId, customerName: "Gamma 100% Rubber"));
        await CreateOrderAsync(admin, NewOrder("XY_400", searchProductId, customerName: "Delta"));

        await AssertListedAsync(admin, new Dictionary<string, string[]>
        {
            ["search=ab-"] = ["AB-100", "AB-200"],
            ["search=BETA"] = ["AB-200"],
            ["search=srch"] = ["AB-100", "XY_400"],
            ["search=duct%20ze"] = ["AB-200", "XY-300"],
            ["search=100"] = ["AB-100", "XY-300"],
            ["search=%20%20beta%20%20"] = ["AB-200"],
            ["search=%20"] = ["AB-100", "AB-200", "XY-300", "XY_400"],
        });
    }

    [Fact]
    public async Task AC_66_percent_and_underscore_in_a_search_text_are_taken_literally()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "LIT");
        await CreateOrderAsync(admin, NewOrder("XY-300", productId, customerName: "Gamma 100% Rubber"));
        await CreateOrderAsync(admin, NewOrder("XY_400", productId, customerName: "Delta 400"));

        await AssertListedAsync(admin, new Dictionary<string, string[]>
        {
            ["search=0%25"] = ["XY-300"],
            ["search=y_"] = ["XY_400"],
            ["customer=0%25"] = ["XY-300"],
        });
    }

    [Fact]
    public async Task AC_67_filters_for_status_late_customer_due_date_range_and_priority_list_only_orders_matching_all_of_them()
    {
        // 2026-11-18 13:00 in Berlin: orders due up to 2026-11-17 and not finished are late.
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString(), clock: clock);
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC67");
        await CreateOrderAsync(admin, NewOrder("SO-1", productId, dueDate: "2026-11-10", priority: "High", customerName: "Tire Shop North"));
        int cancelledOrderId = (await CreateOrderAsync(admin, NewOrder("SO-2", productId, dueDate: "2026-11-12", priority: "Normal", customerName: "Tire Shop North"))).GetProperty("id").GetInt32();
        await CancelOrderAsync(admin, cancelledOrderId);
        await CreateOrderAsync(admin, NewOrder("SO-3", productId, dueDate: "2026-12-01", priority: "High", customerName: "Tire Shop North"));
        await CreateOrderAsync(admin, NewOrder("SO-4", productId, dueDate: "2026-11-11", priority: "High", customerName: "Wheel World"));
        await CreateOrderAsync(admin, NewOrder("SO-5", productId, dueDate: "2026-11-14", priority: "Low", customerName: "Tire Shop North"));

        await AssertListedAsync(admin, new Dictionary<string, string[]>
        {
            ["status=Cancelled"] = ["SO-2"],
            ["status=Pending&status=Cancelled"] = ["SO-1", "SO-4", "SO-2", "SO-5", "SO-3"],
            ["status=Scheduled"] = [],
            ["late=true"] = ["SO-1", "SO-4", "SO-5"],
            ["late=false"] = ["SO-2", "SO-3"],
            ["customer=NORTH"] = ["SO-1", "SO-2", "SO-5", "SO-3"],
            ["dueFrom=2026-11-11&dueTo=2026-11-14"] = ["SO-4", "SO-2", "SO-5"],
            ["dueFrom=2026-11-12"] = ["SO-2", "SO-5", "SO-3"],
            ["dueTo=2026-11-11"] = ["SO-1", "SO-4"],
            ["priority=High"] = ["SO-1", "SO-4", "SO-3"],
            ["priority=High&priority=Low"] = ["SO-1", "SO-4", "SO-5", "SO-3"],
            ["status=Pending&late=true&customer=north&dueFrom=2026-11-01&dueTo=2026-11-30&priority=High"] = ["SO-1"],
        });
    }

    [Fact]
    public async Task AC_65_AC_67_a_status_filter_pages_and_counts_only_the_matching_orders()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC67P");
        foreach ((string orderNo, string dueDate) in new[] { ("SO-1", "2026-12-01"), ("SO-2", "2026-12-02"), ("SO-3", "2026-12-03"), ("SO-4", "2026-12-04") })
        {
            await CreateOrderAsync(admin, NewOrder(orderNo, productId, dueDate: dueDate));
        }

        await CancelOrderAsync(admin, (await admin.GetJsonAsync($"{OrdersPath}?search=SO-2")).GetProperty("items")[0].GetProperty("id").GetInt32());

        JsonElement secondPage = await admin.GetJsonAsync($"{OrdersPath}?status=Pending&pageSize=2&page=2");

        Assert.Equal(["SO-4"], OrderNumbers(secondPage));
        Assert.Equal((2, 2, 3), (secondPage.GetProperty("page").GetInt32(), secondPage.GetProperty("pageSize").GetInt32(), secondPage.GetProperty("totalCount").GetInt32()));
    }

    [Fact]
    public async Task AC_68_orders_sort_by_order_number_customer_priority_due_date_and_creation_time_both_ways()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC68S");
        await CreateOrderAsync(admin, NewOrder("S-1", productId, dueDate: "2026-12-03", priority: "Low", customerName: "Carol"));
        await CreateOrderAsync(admin, NewOrder("S-2", productId, dueDate: "2026-12-01", priority: "High", customerName: "Alice"));
        await CreateOrderAsync(admin, NewOrder("S-3", productId, dueDate: "2026-12-02", priority: "Normal", customerName: "Bob"));

        await AssertListedAsync(admin, new Dictionary<string, string[]>
        {
            ["sort=orderNo"] = ["S-1", "S-2", "S-3"],
            ["sort=-orderNo"] = ["S-3", "S-2", "S-1"],
            ["sort=customerName"] = ["S-2", "S-3", "S-1"],
            ["sort=-customerName"] = ["S-1", "S-3", "S-2"],
            ["sort=priority"] = ["S-1", "S-3", "S-2"],
            ["sort=-priority"] = ["S-2", "S-3", "S-1"],
            ["sort=dueDate"] = ["S-2", "S-3", "S-1"],
            ["sort=-dueDate"] = ["S-1", "S-3", "S-2"],
            ["sort=createdAt"] = ["S-1", "S-2", "S-3"],
            ["sort=-createdAt"] = ["S-3", "S-2", "S-1"],
        });
    }

    [Fact]
    public async Task AC_68_orders_with_the_same_sort_value_follow_the_order_number()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession admin = await factory.SignInAsRoleAsync(UserRole.Admin);
        int productId = await CreateRoutedProductAsync(admin, "AC68T");
        await CreateOrderAsync(admin, NewOrder("T-2", productId, priority: "High"));
        await CreateOrderAsync(admin, NewOrder("T-1", productId, priority: "High"));
        await CreateOrderAsync(admin, NewOrder("T-3", productId, priority: "Low"));

        await AssertListedAsync(admin, new Dictionary<string, string[]>
        {
            ["sort=-priority"] = ["T-1", "T-2", "T-3"],
            ["sort=-dueDate"] = ["T-1", "T-2", "T-3"],
        });
    }

    [Theory]
    [InlineData("sort=status")]
    [InlineData("sort=DueDate")]
    [InlineData("sort=--dueDate")]
    public async Task AC_68_an_unknown_sort_field_is_rejected(string query)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage response = await viewer.GetAsync($"{OrdersPath}?{query}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["sort"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
    }

    [Theory]
    [InlineData("status=Unknown", "status")]
    [InlineData("status=1", "status")]
    [InlineData("status=Pending&status=pending", "status")]
    [InlineData("priority=Urgent", "priority")]
    [InlineData("dueFrom=2026-12-02&dueTo=2026-12-01", "dueTo")]
    public async Task AC_149_an_unknown_status_or_priority_or_a_reversed_due_date_range_is_rejected(string query, string expectedField)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage response = await viewer.GetAsync($"{OrdersPath}?{query}");

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal([expectedField], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
    }

    [Theory]
    [InlineData("late=maybe")]
    [InlineData("dueFrom=tomorrow")]
    [InlineData("dueTo=2026-13-01")]
    public async Task AC_149_a_late_flag_or_due_date_that_cannot_be_read_is_rejected_as_malformed(string query)
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        using ApiSession viewer = await factory.SignInAsRoleAsync(UserRole.Viewer);

        HttpResponseMessage response = await viewer.GetAsync($"{OrdersPath}?{query}");

        await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.RequestMalformed);
    }

    private static async Task AssertListedAsync(ApiSession session, Dictionary<string, string[]> expectedOrderNumbersByQuery)
    {
        foreach ((string query, string[] expectedOrderNumbers) in expectedOrderNumbersByQuery)
        {
            JsonElement page = await session.GetJsonAsync($"{OrdersPath}?{query}");
            Assert.True(
                expectedOrderNumbers.SequenceEqual(OrderNumbers(page)) && page.GetProperty("totalCount").GetInt32() == expectedOrderNumbers.Length,
                $"?{query} listed [{string.Join(", ", OrderNumbers(page))}] of {page.GetProperty("totalCount").GetInt32()}, expected [{string.Join(", ", expectedOrderNumbers)}].");
        }
    }

    private static string[] OrderNumbers(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(order => order.GetProperty("orderNo").GetString() ?? string.Empty).ToArray();
}

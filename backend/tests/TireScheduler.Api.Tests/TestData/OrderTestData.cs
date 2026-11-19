using System.Net;
using System.Text.Json;

namespace TireScheduler.Api.Tests.TestData;

// Orders built through the API, as a planner would. Their master data comes from MasterDataTestData.
internal static class OrderTestData
{
    public const string OrdersPath = "/api/v1/orders";

    public static string OrderPath(int orderId) => $"{OrdersPath}/{orderId}";

    public static object NewOrder(
        string orderNo,
        int productId,
        int quantity = 10,
        string dueDate = "2026-12-01",
        string? priority = null,
        string customerName = "Tire Shop North") =>
        new { orderNo, customerName, productId, quantity, dueDate, priority };

    // A write answers without the order (DEC-29), so the order is read back after it.
    public static async Task<JsonElement> CreateOrderAsync(ApiSession planner, object order)
    {
        HttpResponseMessage response = await planner.PostAsync(OrdersPath, order);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Creating the order answered {response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return await planner.GetJsonAsync(OrderPath((await ApiSession.ReadJsonAsync(response)).GetProperty("id").GetInt32()));
    }

    public static async Task<JsonElement> CancelOrderAsync(ApiSession planner, int orderId)
    {
        HttpResponseMessage response = await planner.PostAsync($"{OrderPath(orderId)}/cancel");
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"Cancelling the order answered {response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return await planner.GetJsonAsync(OrderPath(orderId));
    }

    public static int[] LotIds(JsonElement order) =>
        order.GetProperty("operations").EnumerateArray()
            .SelectMany(operation => operation.GetProperty("lots").EnumerateArray())
            .Select(lot => lot.GetProperty("id").GetInt32())
            .ToArray();

    public static JsonElement Lot(JsonElement order, int lotId) =>
        order.GetProperty("operations").EnumerateArray()
            .SelectMany(operation => operation.GetProperty("lots").EnumerateArray())
            .Single(lot => lot.GetProperty("id").GetInt32() == lotId);

    // The order in a page of the order list.
    public static JsonElement ListedOrder(JsonElement page, int orderId) =>
        Assert.Single(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == orderId);
}

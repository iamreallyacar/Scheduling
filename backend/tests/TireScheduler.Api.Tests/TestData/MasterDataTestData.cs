using System.Text.Json;

namespace TireScheduler.Api.Tests.TestData;

// Master data rows built through the API, as an Admin would; one method per kind of row.
internal static class MasterDataTestData
{
    public const string ResourceTypesPath = "/api/v1/resource-types";
    public const string ResourcesPath = "/api/v1/resources";
    public const string ProductsPath = "/api/v1/products";
    public const string CalendarsPath = "/api/v1/calendars";
    public const string HolidaysPath = "/api/v1/holidays";

    public static string RoutingPath(int productId) => $"{ProductsPath}/{productId}/routing";

    public static string ShiftsPath(int calendarId) => $"{CalendarsPath}/{calendarId}/shifts";

    public static Task<int> CreateResourceTypeAsync(ApiSession admin, string name) => CreatedIdAsync(admin, ResourceTypesPath, new { name });

    public static Task<int> CreateResourceAsync(ApiSession admin, string name, int resourceTypeId) =>
        CreatedIdAsync(admin, ResourcesPath, new { name, resourceTypeId });

    public static Task<int> CreateProductAsync(ApiSession admin, string code, string? name = null) =>
        CreatedIdAsync(admin, ProductsPath, new { code, name = name ?? $"Product {code}" });

    public static object Stage(int sequenceNo, int resourceTypeId, int cycleMinutes = 10, int setupMinutes = 0, int unitsPerCycle = 1) =>
        new { sequenceNo, resourceTypeId, setupMinutes, cycleMinutes, unitsPerCycle };

    public static object Dependency(int predecessorSequenceNo, int successorSequenceNo, int minLagMinutes = 0) =>
        new { predecessorSequenceNo, successorSequenceNo, minLagMinutes };

    public static async Task SaveRoutingAsync(ApiSession admin, int productId, object[] stages, object[] dependencies) =>
        await ApiSession.ReadJsonAsync(await admin.PutAsync(RoutingPath(productId), new { stages, dependencies }));

    // A product with a two-stage routing (building, then curing) on machine types of its own.
    public static async Task<int> CreateRoutedProductAsync(ApiSession admin, string code)
    {
        int buildingTypeId = await CreateResourceTypeAsync(admin, $"{code} building");
        int curingTypeId = await CreateResourceTypeAsync(admin, $"{code} curing");
        int productId = await CreateProductAsync(admin, code);
        await SaveRoutingAsync(admin, productId, [Stage(1, buildingTypeId, cycleMinutes: 5), Stage(2, curingTypeId, cycleMinutes: 12)], [Dependency(1, 2)]);
        return productId;
    }

    // v1 has one calendar, the plant default (A3).
    public static async Task<int> PlantCalendarIdAsync(ApiSession session) =>
        Assert.Single((await session.GetJsonAsync(CalendarsPath)).EnumerateArray()).GetProperty("id").GetInt32();

    public static object Shift(string weekday, string start, string end) => new { weekday, start, end };

    public static async Task SaveShiftsAsync(ApiSession admin, int calendarId, object[] shifts) =>
        await ApiSession.ReadJsonAsync(await admin.PutAsync(ShiftsPath(calendarId), new { shifts }));

    public static Task<int> CreateHolidayAsync(ApiSession admin, string date, string name) => CreatedIdAsync(admin, HolidaysPath, new { date, name });

    private static async Task<int> CreatedIdAsync(ApiSession admin, string path, object body)
    {
        JsonElement created = await ApiSession.ReadJsonAsync(await admin.PostAsync(path, body));
        return created.GetProperty("id").GetInt32();
    }
}

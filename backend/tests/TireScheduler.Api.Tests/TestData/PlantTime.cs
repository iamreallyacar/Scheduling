namespace TireScheduler.Api.Tests.TestData;

// Plant-local times as the UTC instants the API and the database take. The API under test runs in
// SchedulerApiFactory.DefaultPlantTimeZone, so expected times are written as a planner reads them.
internal static class PlantTime
{
    private static readonly TimeZoneInfo PlantZone = TimeZoneInfo.FindSystemTimeZoneById(SchedulerApiFactory.DefaultPlantTimeZone);

    // Most tests use the week of Mon 2026-11-16, so the month and year default to it.
    public static DateTimeOffset Berlin(int day, int hour, int minute = 0, int month = 11, int year = 2026) =>
        new(TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), PlantZone));
}

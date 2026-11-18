namespace TireScheduler.Engine.Tests;

// The plant of acceptance-criteria.md sections 1 and 4: zone Europe/Berlin, the plant default
// calendar (Monday to Friday, three 8-hour shifts), Press 1 and Press 2 of type Curing and
// Builder 1 of type Building, all active. Days are in the week of Monday 2026-11-16 (winter time).
internal static class TestPlant
{
    public const string Berlin = "Europe/Berlin";
    public const int PlantCalendarId = 1;
    public const int CuringType = 1;
    public const int BuildingType = 2;
    public const int Press1 = 11;
    public const int Press2 = 12;
    public const int Builder1 = 13;

    private static readonly TimeZoneInfo BerlinZone = TimeZoneInfo.FindSystemTimeZoneById(Berlin);

    public static ScheduleSnapshot Empty() => new(
        Berlin,
        [new SnapshotCalendar(PlantCalendarId, ThreeShifts(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday))],
        new HashSet<DateOnly>(),
        [
            new SnapshotResource(Press1, CuringType, PlantCalendarId, IsActive: true),
            new SnapshotResource(Press2, CuringType, PlantCalendarId, IsActive: true),
            new SnapshotResource(Builder1, BuildingType, PlantCalendarId, IsActive: true),
        ],
        [],
        [],
        [],
        []);

    public static SnapshotShift[] ThreeShifts(params DayOfWeek[] weekdays) =>
        [.. weekdays.SelectMany(weekday => new[] { new SnapshotShift(weekday, 0, 480), new SnapshotShift(weekday, 480, 960), new SnapshotShift(weekday, 960, 1440) })];

    public static DateTimeOffset Mon(int hour, int minute = 0) => Local(new DateOnly(2026, 11, 16), hour, minute);

    public static DateTimeOffset Tue(int hour, int minute = 0) => Local(new DateOnly(2026, 11, 17), hour, minute);

    public static DateTimeOffset Wed(int hour, int minute = 0) => Local(new DateOnly(2026, 11, 18), hour, minute);

    public static DateTimeOffset Fri(int hour, int minute = 0) => Local(new DateOnly(2026, 11, 20), hour, minute);

    public static DateTimeOffset Sat(int hour, int minute = 0) => Local(new DateOnly(2026, 11, 21), hour, minute);

    public static DateOnly NextMonday { get; } = new(2026, 11, 23);

    public static DateTimeOffset NextMon(int hour, int minute = 0) => Local(NextMonday, hour, minute);

    public static DateTimeOffset NextTue(int hour, int minute = 0) => Local(NextMonday.AddDays(1), hour, minute);

    // Only for times that exist once; the DST tests give their instants in UTC.
    public static DateTimeOffset Local(DateOnly date, int hour, int minute = 0) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, minute)), BerlinZone));

    public static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    // Each lot is its own operation unless the test links lots to a shared operation.
    public static ScheduleSnapshot WithLot(
        this ScheduleSnapshot snapshot,
        int lotId,
        int durationMinutes,
        int resourceTypeId = CuringType,
        int? operationId = null,
        LotProgressState progress = LotProgressState.NotStarted,
        bool orderIsCancelled = false) =>
        snapshot with { Lots = [.. snapshot.Lots, new SnapshotLot(lotId, operationId ?? lotId, durationMinutes, resourceTypeId, progress, orderIsCancelled)] };

    public static ScheduleSnapshot WithPlacement(this ScheduleSnapshot snapshot, int placementId, int lotId, int resourceId, DateTimeOffset plannedStart, DateTimeOffset plannedEnd) =>
        snapshot with { Placements = [.. snapshot.Placements, new SnapshotPlacement(placementId, lotId, resourceId, plannedStart, plannedEnd)] };

    public static ScheduleSnapshot WithUnavailability(this ScheduleSnapshot snapshot, int unavailabilityId, int resourceId, DateTimeOffset start, DateTimeOffset? end) =>
        snapshot with { Unavailabilities = [.. snapshot.Unavailabilities, new SnapshotUnavailability(unavailabilityId, resourceId, start, end)] };

    public static ScheduleSnapshot WithDependency(this ScheduleSnapshot snapshot, int predecessorOperationId, int successorOperationId, int minLagMinutes = 0) =>
        snapshot with { Dependencies = [.. snapshot.Dependencies, new StageDependency(predecessorOperationId, successorOperationId, minLagMinutes)] };

    public static ScheduleSnapshot WithHoliday(this ScheduleSnapshot snapshot, DateOnly date) =>
        snapshot with { Holidays = new HashSet<DateOnly>(snapshot.Holidays) { date } };

    public static ScheduleSnapshot WithInactive(this ScheduleSnapshot snapshot, int resourceId) =>
        snapshot with { Resources = [.. snapshot.Resources.Select(resource => resource.Id == resourceId ? resource with { IsActive = false } : resource)] };

    public static ScheduleSnapshot WithShifts(this ScheduleSnapshot snapshot, params SnapshotShift[] shifts) =>
        snapshot with { Calendars = [new SnapshotCalendar(PlantCalendarId, shifts)] };

    public static PlacementCheck Place(this ScheduleSnapshot snapshot, int lotId, int resourceId, DateTimeOffset plannedStart) =>
        PlacementRules.Validate(snapshot, new PlacementRequest(lotId, resourceId, plannedStart));
}

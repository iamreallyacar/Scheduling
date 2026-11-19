namespace TireScheduler.Engine;

// Everything the rules need for one schedule version (engine README, section "Inputs"). The
// caller loads it; the engine reads no database and no clock. Times are UTC instants, except the
// shifts and holidays, which are plant-local and converted here (ARCHITECTURE.md section "Time rules").
public sealed record ScheduleSnapshot(
    string PlantTimeZoneId,
    IReadOnlyList<SnapshotCalendar> Calendars,
    IReadOnlySet<DateOnly> Holidays,
    IReadOnlyList<SnapshotResource> Resources,
    IReadOnlyList<SnapshotUnavailability> Unavailabilities,
    IReadOnlyList<SnapshotLot> Lots,
    IReadOnlyList<StageDependency> Dependencies,
    IReadOnlyList<SnapshotPlacement> Placements);

public sealed record SnapshotCalendar(int Id, IReadOnlyList<SnapshotShift> Shifts);

// Minutes from plant-local midnight; a shift never crosses midnight, so the end is at most 1440 (A18).
public sealed record SnapshotShift(DayOfWeek Weekday, int StartMinuteOfDay, int EndMinuteOfDay);

// The name breaks ties between equally good slots on different resources (AC-118).
public sealed record SnapshotResource(int Id, string Name, int ResourceTypeId, int CalendarId, bool IsActive);

// No end means the resource is blocked from the start onward (R3).
public sealed record SnapshotUnavailability(int Id, int ResourceId, DateTimeOffset Start, DateTimeOffset? End);

// The sequence number is the lot's stage in its order's routing; it breaks ties between stages
// that may be proposed in either order (AC-129).
public sealed record SnapshotLot(
    int Id,
    int OperationId,
    int SequenceNo,
    int DurationMinutes,
    int ResourceTypeId,
    LotProgressState Progress,
    bool OrderIsCancelled);

public enum LotProgressState
{
    NotStarted,
    InProgress,
    Completed,
}

// Links operations, so every lot of the successor waits for every lot of the predecessor (A11).
public sealed record StageDependency(int PredecessorOperationId, int SuccessorOperationId, int MinLagMinutes);

// The stored planned end, which is recomputed when the calendar or the holidays change (A2).
public sealed record SnapshotPlacement(int Id, int LotId, int ResourceId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd);

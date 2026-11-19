using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// The only place where the plant calendars and holidays of MasterData become engine calendars, so
// the snapshot (R6) and the timeline's working periods read the same working time (DEC-49).
internal sealed class EngineWorkingTimeLoader(IMasterDataReader masterData)
{
    public async Task<EngineWorkingTime> LoadAsync(CancellationToken cancellationToken)
    {
        PlantWorkingTime workingTime = await masterData.GetWorkingTimeAsync(cancellationToken);
        return new EngineWorkingTime
        {
            TimeZoneId = masterData.PlantTimeZoneId,
            Calendars =
            [
                .. workingTime.Calendars.Select(calendar => new SnapshotCalendar(
                    calendar.Id,
                    [.. calendar.Shifts.Select(shift => new SnapshotShift(shift.Weekday, shift.StartMinuteOfDay, shift.EndMinuteOfDay))])),
            ],
            Holidays = workingTime.Holidays.ToHashSet(),
        };
    }
}

// Everything the engine needs to read working time.
internal sealed record EngineWorkingTime
{
    public required string TimeZoneId { get; init; }

    public required IReadOnlyList<SnapshotCalendar> Calendars { get; init; }

    public required IReadOnlySet<DateOnly> Holidays { get; init; }
}

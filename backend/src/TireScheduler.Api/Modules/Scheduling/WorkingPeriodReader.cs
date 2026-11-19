using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

internal sealed class WorkingPeriodReader(EngineWorkingTimeLoader workingTimeLoader) : IWorkingPeriodReader
{
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<WorkingPeriod>>> WorkingPeriodsByCalendarAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken)
    {
        EngineWorkingTime workingTime = await workingTimeLoader.LoadAsync(cancellationToken);
        return workingTime.Calendars.ToDictionary(
            calendar => calendar.Id,
            calendar => WorkingPeriods.Between(calendar, workingTime.Holidays, workingTime.TimeZoneId, rangeStart, rangeEnd));
    }
}

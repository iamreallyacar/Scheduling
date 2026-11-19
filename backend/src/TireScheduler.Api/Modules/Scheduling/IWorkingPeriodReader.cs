using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// The public read interface of Scheduling for the timeline, which shades the time outside the
// working periods (DEC-45). On the server only the engine reads the plant-local shift times
// (ARCHITECTURE.md section "Time rules"), and Scheduling turns the calendars into engine calendars
// (DEC-49).
public interface IWorkingPeriodReader
{
    // The working periods of every calendar in the range, by calendar id, as R6 reads them (engine
    // README, "Working time").
    Task<IReadOnlyDictionary<int, IReadOnlyList<WorkingPeriod>>> WorkingPeriodsByCalendarAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken);
}

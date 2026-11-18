using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// F-MD-5: the calendars and their weekly shifts. v1 has only the plant default (A3).
internal sealed class CalendarRegister(MasterDataDbContext context, RequestTransaction requestTransaction, IOptions<PlantOptions> plantOptions)
{
    public async Task<IReadOnlyList<CalendarResponse>> ListAsync(CancellationToken cancellationToken)
    {
        List<Calendar> calendars = await context.Calendars
            .Include(calendar => calendar.Shifts)
            .OrderBy(calendar => calendar.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return calendars.Select(ToResponse).ToList();
    }

    public async Task<CalendarResponse> ReplaceShiftsAsync(int calendarId, CalendarShiftsRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<CalendarShiftRequest> shiftRequests = request.Shifts.Validated();
        RequestedShift[] shifts = shiftRequests
            .Select((shift, position) => new RequestedShift(
                position,
                shift.Weekday.Validated(),
                ShiftTimes.ToMinuteOfDay(shift.Start.Validated()),
                ShiftTimes.ToMinuteOfDay(shift.End.Validated())))
            .ToArray();
        Dictionary<string, IReadOnlyList<string>> fieldErrors = ShiftRules.FindFieldErrors(shifts);
        if (fieldErrors.Count > 0)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }

        await using (OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken))
        {
            // Two saves of the same calendar run one after the other.
            if (!await context.LockRowAsync<Calendar>(calendarId, cancellationToken))
            {
                throw ApiProblemException.NotFound("This calendar");
            }

            await context.Set<CalendarShift>().Where(shift => shift.CalendarId == calendarId).ExecuteDeleteAsync(cancellationToken);
            context.Set<CalendarShift>().AddRange(shifts.Select(shift => new CalendarShift
            {
                CalendarId = calendarId,
                Weekday = shift.Weekday,
                StartMinuteOfDay = shift.StartMinuteOfDay,
                EndMinuteOfDay = shift.EndMinuteOfDay,
            }));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return (await ListAsync(cancellationToken)).Single(calendar => calendar.Id == calendarId);
    }

    private CalendarResponse ToResponse(Calendar calendar) => new()
    {
        Id = calendar.Id,
        Name = calendar.Name,
        IsPlantDefault = calendar.IsPlantDefault,
        // Every calendar is in the plant time zone (AC-41); the validator has checked the setting at startup.
        TimeZone = plantOptions.Value.TimeZone!,
        Shifts = calendar.Shifts
            .OrderBy(shift => ShiftTimes.WeekdayOrder(shift.Weekday))
            .ThenBy(shift => shift.StartMinuteOfDay)
            .Select(shift => new CalendarShiftResponse
            {
                Weekday = shift.Weekday,
                Start = ShiftTimes.FromMinuteOfDay(shift.StartMinuteOfDay),
                End = ShiftTimes.FromMinuteOfDay(shift.EndMinuteOfDay),
            })
            .ToList(),
    };
}

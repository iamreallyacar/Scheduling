using Microsoft.AspNetCore.Http.HttpResults;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// No endpoint creates or deletes a calendar: v1 has exactly one (A3, AC-44).
internal static class CalendarEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder calendars = api.MapGroup("/calendars").WithTags("Calendar");
        calendars.MapGet("/", async (CalendarRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ListAsync(cancellationToken)))
            .WithName("ListCalendars")
            .RequireAuthorization(AccessPolicies.Read);
        calendars.MapPut("/{id:int}/shifts", async (int id, CalendarShiftsRequest request, CalendarRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ReplaceShiftsAsync(id, request, cancellationToken)))
            .WithName("ReplaceCalendarShifts")
            .RequireAuthorization(AccessPolicies.Administer);

        RouteGroupBuilder holidays = api.MapGroup("/holidays").WithTags("Calendar");
        holidays.MapGet("/", async (HolidayRegister register, CancellationToken cancellationToken) =>
                TypedResults.Ok(await register.ListAsync(cancellationToken)))
            .WithName("ListHolidays")
            .RequireAuthorization(AccessPolicies.Read);
        holidays.MapPost("/", async Task<Created<HolidayResponse>> (HolidayRequest request, HolidayRegister register, CancellationToken cancellationToken) =>
            {
                HolidayResponse created = await register.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/v1/holidays/{created.Id}", created);
            })
            .WithName("CreateHoliday")
            .RequireAuthorization(AccessPolicies.Administer);
        holidays.MapDelete("/{id:int}", async (int id, HolidayRegister register, CancellationToken cancellationToken) =>
            {
                await register.DeleteAsync(id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeleteHoliday")
            .RequireAuthorization(AccessPolicies.Administer);
    }
}

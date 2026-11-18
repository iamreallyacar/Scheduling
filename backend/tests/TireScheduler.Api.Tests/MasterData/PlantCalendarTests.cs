using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;
using static TireScheduler.Api.Tests.TestData.MasterDataTestData;

namespace TireScheduler.Api.Tests.MasterData;

// F-MD-5 through the API: the plant calendar, its shifts and the holiday list.
public sealed class PlantCalendarTests(SchedulerApiFixture api) : IClassFixture<SchedulerApiFixture>
{
    [Fact]
    public async Task AC_144_every_role_reads_the_calendar_with_its_time_zone_and_shifts_monday_first_then_by_start()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);
        await SaveShiftsAsync(admin, calendarId,
        [
            Shift("Sunday", "06:00", "14:00"),
            Shift("Monday", "16:00", "24:00"),
            Shift("Monday", "00:00", "08:00"),
            Shift("Saturday", "08:00", "12:00"),
        ]);

        foreach (UserRole role in new[] { UserRole.Viewer, UserRole.Planner })
        {
            using ApiSession reader = await api.Factory.SignInAsRoleAsync(role);
            JsonElement calendar = Assert.Single((await reader.GetJsonAsync(CalendarsPath)).EnumerateArray());

            Assert.Equal(calendarId, calendar.GetProperty("id").GetInt32());
            Assert.Equal("Plant default", calendar.GetProperty("name").GetString());
            Assert.True(calendar.GetProperty("isPlantDefault").GetBoolean());
            Assert.Equal(SchedulerApiFactory.DefaultPlantTimeZone, calendar.GetProperty("timeZone").GetString());
            Assert.Equal(
                [("Monday", "00:00", "08:00"), ("Monday", "16:00", "24:00"), ("Saturday", "08:00", "12:00"), ("Sunday", "06:00", "14:00")],
                calendar.GetProperty("shifts").EnumerateArray().Select(ShiftValues));
        }
    }

    [Fact]
    public async Task AC_42_admin_saves_shifts_and_they_replace_all_previous_shifts()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);
        await SaveShiftsAsync(admin, calendarId, [Shift("Monday", "00:00", "08:00"), Shift("Tuesday", "00:00", "08:00")]);

        // Touching shifts are allowed, and the same times on another weekday do not overlap.
        HttpResponseMessage response = await admin.PutAsync(ShiftsPath(calendarId), new
        {
            shifts = new[] { Shift("Wednesday", "08:00", "16:00"), Shift("Wednesday", "16:00", "24:00"), Shift("Thursday", "08:00", "16:00") },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        (string, string, string)[] expected = [("Wednesday", "08:00", "16:00"), ("Wednesday", "16:00", "24:00"), ("Thursday", "08:00", "16:00")];
        Assert.Equal(expected, (await ApiSession.ReadJsonAsync(response)).GetProperty("shifts").EnumerateArray().Select(ShiftValues));
        Assert.Equal(expected, (await ReadCalendarAsync(admin)).GetProperty("shifts").EnumerateArray().Select(ShiftValues));
    }

    [Fact]
    public async Task AC_42_a_calendar_saved_without_shifts_has_no_working_time()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);

        await SaveShiftsAsync(admin, calendarId, []);

        Assert.Equal(0, (await ReadCalendarAsync(admin)).GetProperty("shifts").GetArrayLength());
    }

    [Theory]
    [InlineData("08:00", "08:00", "shifts[1].end")]
    [InlineData("16:00", "08:00", "shifts[1].end")]
    [InlineData("08:00", "24:30", "shifts[1].end")]
    [InlineData("24:00", "24:00", "shifts[1].end")]
    [InlineData("25:00", "23:00", "shifts[1].start")]
    [InlineData("8:00", "16:00", "shifts[1].start")]
    [InlineData("07:00", "12:00", "shifts[1].start")]
    public async Task AC_42_shifts_that_do_not_start_before_they_end_end_after_24_00_or_overlap_are_rejected(string start, string end, string expectedField)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);
        object[] savedShifts = [Shift("Friday", "00:00", "08:00")];
        await SaveShiftsAsync(admin, calendarId, savedShifts);

        HttpResponseMessage response = await admin.PutAsync(ShiftsPath(calendarId), new
        {
            shifts = new[] { Shift("Monday", "00:00", "08:00"), Shift("Monday", start, end) },
        });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal([expectedField], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
        Assert.Equal([("Friday", "00:00", "08:00")], (await ReadCalendarAsync(admin)).GetProperty("shifts").EnumerateArray().Select(ShiftValues));
    }

    [Fact]
    public async Task AC_21_shifts_with_missing_values_are_reported_by_field_path()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);

        HttpResponseMessage withoutList = await admin.PutAsync(ShiftsPath(calendarId), new { });
        HttpResponseMessage withEmptyShift = await admin.PutAsync(ShiftsPath(calendarId), new { shifts = new[] { new { } } });

        JsonElement listProblem = await ProblemAssert.HasProblemAsync(withoutList, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["shifts"], listProblem.GetProperty("errors").EnumerateObject().Select(field => field.Name));
        JsonElement shiftProblem = await ProblemAssert.HasProblemAsync(withEmptyShift, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(
            ["shifts[0].end", "shifts[0].start", "shifts[0].weekday"],
            shiftProblem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AC_43_admin_adds_and_deletes_a_holiday_and_holidays_are_listed_by_date()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage created = await admin.PostAsync(HolidaysPath, new { date = "2030-12-25", name = "  Christmas Day " });
        int boxingDayId = await CreateHolidayAsync(admin, "2030-12-26", "Boxing Day");
        await CreateHolidayAsync(admin, "2030-01-01", "New Year");
        HttpResponseMessage deleted = await admin.DeleteAsync($"{HolidaysPath}/{boxingDayId}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement christmas = await ApiSession.ReadJsonAsync(created);
        Assert.Equal(("2030-12-25", "Christmas Day"), HolidayValues(christmas));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using ApiSession viewer = await api.Factory.SignInAsRoleAsync(UserRole.Viewer);
        Assert.Equal(
            [("2030-01-01", "New Year"), ("2030-12-25", "Christmas Day")],
            (await viewer.GetJsonAsync(HolidaysPath)).EnumerateArray().Select(HolidayValues).Where(holiday => holiday.Date.StartsWith("2030", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AC_24_AC_43_a_second_holiday_on_the_same_date_is_rejected_naming_the_date()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        await CreateHolidayAsync(admin, "2031-05-01", "Labour Day");

        HttpResponseMessage response = await admin.PostAsync(HolidaysPath, new { date = "2031-05-01", name = "May Day" });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.Conflict, ProblemCodes.ConflictDuplicate);
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
        Assert.Single((await admin.GetJsonAsync(HolidaysPath)).EnumerateArray(), holiday => holiday.GetProperty("date").GetString() == "2031-05-01");
    }

    [Fact]
    public async Task AC_21_holiday_without_date_or_name_is_rejected()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage response = await admin.PostAsync(HolidaysPath, new { name = " " });

        JsonElement problem = await ProblemAssert.HasProblemAsync(response, HttpStatusCode.BadRequest, ProblemCodes.ValidationFailed);
        Assert.Equal(["date", "name"], problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AC_44_there_is_no_endpoint_that_creates_or_deletes_a_calendar()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);

        HttpResponseMessage response = await admin.PostAsync(CalendarsPath, new { name = "Second calendar" });

        Assert.False(response.IsSuccessStatusCode, $"Creating a calendar answered {response.StatusCode}.");
        string[] calendarWrites = api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(CalendarsPath, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Where(endpoint => !endpoint.StartsWith("GET ", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["PUT /api/v1/calendars/{id:int}/shifts"], calendarWrites);
        Assert.Single((await admin.GetJsonAsync(CalendarsPath)).EnumerateArray());
    }

    [Fact]
    public async Task AC_23_unknown_calendar_or_holiday_is_not_found()
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        const int unknownId = 987654;

        HttpResponseMessage shifts = await admin.PutAsync(ShiftsPath(unknownId), new { shifts = Array.Empty<object>() });
        HttpResponseMessage holiday = await admin.DeleteAsync($"{HolidaysPath}/{unknownId}");

        await ProblemAssert.HasProblemAsync(shifts, HttpStatusCode.NotFound, ProblemCodes.NotFound);
        await ProblemAssert.HasProblemAsync(holiday, HttpStatusCode.NotFound, ProblemCodes.NotFound);
    }

    [Theory]
    [InlineData(UserRole.Planner)]
    [InlineData(UserRole.Viewer)]
    public async Task AC_10_AC_12_viewer_and_planner_cannot_change_shifts_or_holidays(UserRole role)
    {
        using ApiSession admin = await api.Factory.SignInAsRoleAsync(UserRole.Admin);
        int calendarId = await PlantCalendarIdAsync(admin);
        int holidayId = await CreateHolidayAsync(admin, role == UserRole.Planner ? "2032-02-01" : "2032-03-01", $"Holiday for {role}");
        using ApiSession session = await api.Factory.SignInAsRoleAsync(role);

        HttpResponseMessage shifts = await session.PutAsync(ShiftsPath(calendarId), new { shifts = Array.Empty<object>() });
        HttpResponseMessage addHoliday = await session.PostAsync(HolidaysPath, new { date = "2032-12-24", name = "Christmas Eve" });
        HttpResponseMessage deleteHoliday = await session.DeleteAsync($"{HolidaysPath}/{holidayId}");

        await ProblemAssert.HasProblemAsync(shifts, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        await ProblemAssert.HasProblemAsync(addHoliday, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
        await ProblemAssert.HasProblemAsync(deleteHoliday, HttpStatusCode.Forbidden, ProblemCodes.AuthForbidden);
    }

    private static async Task<JsonElement> ReadCalendarAsync(ApiSession session) =>
        Assert.Single((await session.GetJsonAsync(CalendarsPath)).EnumerateArray());

    private static (string Weekday, string Start, string End) ShiftValues(JsonElement shift) =>
        (shift.GetProperty("weekday").GetString()!, shift.GetProperty("start").GetString()!, shift.GetProperty("end").GetString()!);

    private static (string Date, string Name) HolidayValues(JsonElement holiday) =>
        (holiday.GetProperty("date").GetString()!, holiday.GetProperty("name").GetString()!);
}

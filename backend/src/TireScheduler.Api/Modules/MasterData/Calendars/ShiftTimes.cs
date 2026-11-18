using System.Globalization;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// A shift time in the API is plant-local "HH:mm"; 24:00 is the midnight that ends a day (AC-144).
internal static class ShiftTimes
{
    public const string Pattern = "^(([01][0-9]|2[0-3]):[0-5][0-9]|24:00)$";
    public const string FormatMessage = "Enter a time as HH:mm between 00:00 and 24:00.";

    // Expects a value that matches Pattern.
    public static int ToMinuteOfDay(string time) =>
        (int.Parse(time[..2], CultureInfo.InvariantCulture) * 60) + int.Parse(time[3..], CultureInfo.InvariantCulture);

    public static string FromMinuteOfDay(int minuteOfDay) =>
        string.Create(CultureInfo.InvariantCulture, $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}");

    // Monday first, as planners read a week.
    public static int WeekdayOrder(DayOfWeek weekday) => ((int)weekday + 6) % 7;
}

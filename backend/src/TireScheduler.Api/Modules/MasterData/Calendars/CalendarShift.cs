namespace TireScheduler.Api.Modules.MasterData.Calendars;

// Working time on one weekday, in plant-local wall-clock time (A2). Stored as minutes after
// midnight because a shift may end at 24:00 (minute 1440), which a time of day cannot hold.
// A shift never crosses midnight (A18).
internal sealed class CalendarShift
{
    public const int MinutesPerDay = 24 * 60;

    public int CalendarId { get; set; }

    public DayOfWeek Weekday { get; set; }

    public int StartMinuteOfDay { get; set; }

    public int EndMinuteOfDay { get; set; }
}

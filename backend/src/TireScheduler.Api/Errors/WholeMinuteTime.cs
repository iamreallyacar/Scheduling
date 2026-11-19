namespace TireScheduler.Api.Errors;

// Request times are whole minutes (A2, A40). Every register that takes a time checks it here, so
// the rule and its message are written once.
internal static class WholeMinuteTime
{
    // The field error for a time that is not a whole minute, or null for one that is. timeName is
    // how the message names the time, for example "start".
    public static string? ErrorOf(DateTimeOffset time, string timeName) =>
        time.Ticks % TimeSpan.TicksPerMinute == 0 ? null : $"The {timeName} must be a whole minute.";
}

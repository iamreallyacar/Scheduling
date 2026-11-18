using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Queries.Schedule;

// The plant-local days the timeline shows, both included (DEC-45, A44).
internal sealed record TimelineRange
{
    public const int MaxDays = 31;

    // A plant schedule never leaves these years, and the day arithmetic around the range then
    // never reaches the ends of the calendar (A44).
    private static readonly DateOnly EarliestDay = new(2000, 1, 1);
    private static readonly DateOnly LatestDay = new(2999, 12, 31);

    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    private static string OutsideSupportedYears => $"The day must be in the years {EarliestDay.Year} to {LatestDay.Year}.";

    // Every field error at once (AC-187), or the checked range.
    public static TimelineRange Check(DateOnly? from, DateOnly? to)
    {
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        if (from is null)
        {
            fieldErrors["from"] = ["The first day of the range is required."];
        }
        else if (from < EarliestDay || from > LatestDay)
        {
            fieldErrors["from"] = [OutsideSupportedYears];
        }

        if (to is null)
        {
            fieldErrors["to"] = ["The last day of the range is required."];
        }
        else if (to < EarliestDay || to > LatestDay)
        {
            fieldErrors["to"] = [OutsideSupportedYears];
        }
        else if (to < from)
        {
            fieldErrors["to"] = ["The last day of the range must not be before its first day."];
        }
        else if (from is { } firstDay && to.Value.DayNumber - firstDay.DayNumber + 1 > MaxDays)
        {
            fieldErrors["to"] = [$"The range must not be longer than {MaxDays} days."];
        }

        if (fieldErrors.Count > 0 || from is not { } checkedFrom || to is not { } checkedTo)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }

        return new TimelineRange { From = checkedFrom, To = checkedTo };
    }
}

using System.ComponentModel.DataAnnotations;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// The shifts of a calendar are saved as one weekly list (A26).
public sealed record CalendarShiftsRequest
{
    [Required]
    public IReadOnlyList<CalendarShiftRequest>? Shifts { get; init; }
}

public sealed record CalendarShiftRequest
{
    [Required]
    public DayOfWeek? Weekday { get; init; }

    [Required]
    [RegularExpression(ShiftTimes.Pattern, ErrorMessage = ShiftTimes.FormatMessage)]
    public string? Start { get; init; }

    [Required]
    [RegularExpression(ShiftTimes.Pattern, ErrorMessage = ShiftTimes.FormatMessage)]
    public string? End { get; init; }
}

public sealed record CalendarResponse
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required bool IsPlantDefault { get; init; }

    public required string TimeZone { get; init; }

    public required IReadOnlyList<CalendarShiftResponse> Shifts { get; init; }
}

public sealed record CalendarShiftResponse
{
    public required DayOfWeek Weekday { get; init; }

    public required string Start { get; init; }

    public required string End { get; init; }
}

public sealed record HolidayRequest
{
    [Required]
    public DateOnly? Date { get; init; }

    [Required]
    [StringLength(Holiday.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}

public sealed record HolidayResponse
{
    public required int Id { get; init; }

    public required DateOnly Date { get; init; }

    public required string Name { get; init; }
}

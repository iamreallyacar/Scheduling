namespace TireScheduler.Engine.Tests;

// The late flag of domain-model.md section 6, in the plant time zone of acceptance-criteria.md section 1.
public sealed class OrderLatenessTests
{
    private const string Berlin = "Europe/Berlin";
    private static readonly DateOnly DueDate = new(2026, 11, 10);

    // 2026-11-11 00:00 in Berlin.
    private static readonly DateTimeOffset EndOfDueDay = new(2026, 11, 10, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AC_55_an_unfinished_order_is_not_late_at_23_59_plant_time_on_its_due_day()
    {
        Assert.False(OrderLateness.IsLate(Unfinished(now: EndOfDueDay.AddMinutes(-1))));
    }

    [Fact]
    public void AC_56_an_unfinished_order_is_late_from_00_00_plant_time_after_its_due_day()
    {
        Assert.True(OrderLateness.IsLate(Unfinished(now: EndOfDueDay)));
    }

    [Fact]
    public void AC_57_an_unfinished_order_with_a_lot_planned_to_end_after_its_due_day_is_late_before_that_day()
    {
        DateTimeOffset dayBefore = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

        Assert.True(OrderLateness.IsLate(Unfinished(now: dayBefore, plannedEnds: [EndOfDueDay.AddHours(-5), EndOfDueDay.AddMinutes(1)])));
    }

    [Fact]
    public void AC_57_A27_a_lot_planned_to_end_exactly_at_the_end_of_the_due_day_is_on_time()
    {
        DateTimeOffset dayBefore = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

        Assert.False(OrderLateness.IsLate(Unfinished(now: dayBefore, plannedEnds: [EndOfDueDay])));
    }

    [Fact]
    public void AC_54_a_cancelled_order_is_never_late()
    {
        LatenessFacts overdue = Unfinished(now: EndOfDueDay.AddDays(5), plannedEnds: [EndOfDueDay.AddDays(1)]) with { IsCancelled = true };

        Assert.False(OrderLateness.IsLate(overdue));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void AC_60_a_completed_order_is_late_when_its_last_lot_ended_after_its_due_day(int minutesAfterEndOfDueDay, bool expectedLate)
    {
        LatenessFacts completed = Unfinished(now: EndOfDueDay.AddDays(5)) with { CompletedAt = EndOfDueDay.AddMinutes(minutesAfterEndOfDueDay) };

        Assert.Equal(expectedLate, OrderLateness.IsLate(completed));
    }

    [Fact]
    public void AC_60_the_completion_time_is_the_last_actual_end_once_every_lot_has_one()
    {
        DateTimeOffset firstEnd = EndOfDueDay.AddHours(-3);
        DateTimeOffset lastEnd = EndOfDueDay.AddHours(2);

        Assert.Equal(lastEnd, OrderLateness.CompletionTime([lastEnd, firstEnd]));
        Assert.Null(OrderLateness.CompletionTime([firstEnd, null]));
        Assert.Null(OrderLateness.CompletionTime([]));
    }

    private static LatenessFacts Unfinished(DateTimeOffset now, DateTimeOffset[]? plannedEnds = null) =>
        new(DueDate, Berlin, now, IsCancelled: false, PlannedEnds: plannedEnds ?? [], CompletedAt: null);
}

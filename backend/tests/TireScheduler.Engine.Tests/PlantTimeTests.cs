namespace TireScheduler.Engine.Tests;

// A due date ends at the first instant of the next plant-local day (DEC-22, A27).
public sealed class PlantTimeTests
{
    [Fact]
    public void AC_56_a_berlin_day_in_winter_ends_at_23_00_utc()
    {
        Assert.Equal(Utc(2026, 11, 10, 23, 0), PlantTime.EndOfDay(new DateOnly(2026, 11, 10), "Europe/Berlin"));
    }

    [Fact]
    public void AC_56_a_berlin_day_in_summer_ends_at_22_00_utc()
    {
        Assert.Equal(Utc(2026, 7, 10, 22, 0), PlantTime.EndOfDay(new DateOnly(2026, 7, 10), "Europe/Berlin"));
    }

    [Fact]
    public void AC_56_a_berlin_spring_forward_day_with_23_hours_ends_at_the_next_local_midnight()
    {
        Assert.Equal(Utc(2026, 3, 29, 22, 0), PlantTime.EndOfDay(new DateOnly(2026, 3, 29), "Europe/Berlin"));
    }

    [Fact]
    public void AC_56_a_berlin_fall_back_day_with_25_hours_ends_at_the_next_local_midnight()
    {
        Assert.Equal(Utc(2026, 10, 25, 23, 0), PlantTime.EndOfDay(new DateOnly(2026, 10, 25), "Europe/Berlin"));
    }

    // In Santiago the clocks jump from 2026-09-06 00:00 straight to 01:00.
    [Fact]
    public void A27_where_dst_skips_local_midnight_the_day_ends_at_the_first_local_time_after_the_gap()
    {
        DateTimeOffset end = PlantTime.EndOfDay(new DateOnly(2026, 9, 5), "America/Santiago");

        Assert.Equal(Utc(2026, 9, 6, 4, 0), end);
        Assert.Equal(new DateTime(2026, 9, 6, 1, 0, 0), TimeZoneInfo.ConvertTime(end, TimeZoneInfo.FindSystemTimeZoneById("America/Santiago")).DateTime);
    }

    // In the Azores the clocks go back from 2026-10-25 01:00 to 00:00, so local midnight occurs twice.
    [Fact]
    public void A27_where_local_midnight_occurs_twice_the_day_ends_at_the_first_occurrence()
    {
        Assert.Equal(Utc(2026, 10, 25, 0, 0), PlantTime.EndOfDay(new DateOnly(2026, 10, 24), "Atlantic/Azores"));
    }

    [Fact]
    public void A33_a_plant_local_time_in_winter_is_one_hour_ahead_of_utc_in_berlin()
    {
        Assert.Equal(Utc(2026, 11, 17, 5, 0), PlantTime.LocalToUtc(new DateTime(2026, 11, 17, 6, 0, 0), "Europe/Berlin"));
    }

    [Fact]
    public void A27_a_plant_local_time_that_occurs_twice_is_its_first_occurrence()
    {
        Assert.Equal(Utc(2026, 10, 25, 0, 30), PlantTime.LocalToUtc(new DateTime(2026, 10, 25, 2, 30, 0), "Europe/Berlin"));
    }

    [Fact]
    public void A27_a_plant_local_time_inside_a_dst_gap_is_the_first_time_after_the_gap()
    {
        Assert.Equal(Utc(2027, 3, 28, 1, 0), PlantTime.LocalToUtc(new DateTime(2027, 3, 28, 2, 30, 0), "Europe/Berlin"));
    }

    [Fact]
    public void An_unknown_time_zone_id_is_an_error()
    {
        Assert.Throws<TimeZoneNotFoundException>(() => PlantTime.EndOfDay(new DateOnly(2026, 11, 10), "Mars/Olympus_Mons"));
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}

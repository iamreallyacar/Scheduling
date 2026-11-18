namespace TireScheduler.Engine.Tests;

// domain-model.md section 4: durationMinutes = setupMinutes + ceil(quantity / unitsPerCycle) x cycleMinutes.
public sealed class LotDurationTests
{
    [Fact]
    public void AC_63_a_curing_lot_of_101_with_setup_30_cycle_12_and_2_units_per_cycle_takes_642_minutes()
    {
        Assert.Equal(642, LotDuration.Minutes(setupMinutes: 30, cycleMinutes: 12, unitsPerCycle: 2, quantity: 101));
    }

    [Fact]
    public void AC_64_a_lot_of_40_with_the_defaults_and_cycle_5_takes_200_minutes()
    {
        Assert.Equal(200, LotDuration.Minutes(setupMinutes: 0, cycleMinutes: 5, unitsPerCycle: 1, quantity: 40));
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    public void A_started_cycle_counts_as_a_whole_cycle(int quantity, int expectedCycles)
    {
        Assert.Equal(expectedCycles * 10, LotDuration.Minutes(setupMinutes: 0, cycleMinutes: 10, unitsPerCycle: 4, quantity: quantity));
    }

    [Fact]
    public void AC_145_the_largest_values_do_not_overflow()
    {
        long expected = int.MaxValue + ((long)int.MaxValue * int.MaxValue);

        Assert.Equal(expected, LotDuration.Minutes(setupMinutes: int.MaxValue, cycleMinutes: int.MaxValue, unitsPerCycle: 1, quantity: int.MaxValue));
    }

    [Theory]
    [InlineData(-1, 1, 1, 1)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(0, 1, 1, 0)]
    public void Values_outside_their_ranges_are_refused(int setupMinutes, int cycleMinutes, int unitsPerCycle, int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LotDuration.Minutes(setupMinutes, cycleMinutes, unitsPerCycle, quantity));
    }
}

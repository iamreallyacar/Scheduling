namespace TireScheduler.PlantGenerator.Tests;

public sealed class SeededRandomTests
{
    // The published SplitMix64 outputs for seed 0. If these change, every named seed names a
    // different plant (AC-151).
    [Fact]
    public void AC_151_the_sequence_for_a_seed_is_the_published_splitmix64_sequence()
    {
        SeededRandom random = new(0);

        Assert.Equal([0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL], [random.NextUInt64(), random.NextUInt64(), random.NextUInt64()]);
    }

    [Fact]
    public void Draws_stay_inside_their_ranges_and_reach_both_ends()
    {
        SeededRandom random = new(42);

        int[] draws = [.. Enumerable.Range(0, 1000).Select(_ => random.Draw(new ValueRange(-2, 2)))];
        double[] fractions = [.. Enumerable.Range(0, 1000).Select(_ => random.NextDouble())];

        Assert.Equal([-2, -1, 0, 1, 2], draws.Distinct().Order());
        Assert.All(fractions, fraction => Assert.InRange(fraction, 0, 0.9999999999));
        Assert.Equal(int.MaxValue, new SeededRandom(1).Draw(new ValueRange(int.MaxValue, int.MaxValue)));
    }
}

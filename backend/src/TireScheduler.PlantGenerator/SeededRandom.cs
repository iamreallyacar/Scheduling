namespace TireScheduler.PlantGenerator;

// SplitMix64 (Steele, Lea and Flood, 2014). System.Random is not promised to give the same
// sequence for a seed across .NET versions; a seed must keep naming the same plant (AC-151).
// Not for security.
public sealed class SeededRandom(long seed)
{
    private ulong state = unchecked((ulong)seed);

    public ulong NextUInt64()
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong mixed = state;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            return mixed ^ (mixed >> 31);
        }
    }

    // From minimum up to, but not including, the end. The ranges here are far below 2^64, so
    // the remainder's bias is too small to matter.
    public int Next(int minimum, int end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(end, minimum);
        ulong span = (ulong)((long)end - minimum);
        return (int)(minimum + (long)(NextUInt64() % span));
    }

    public int Next(int end) => Next(0, end);

    // From 0 up to, but not including, 1, from the top 53 bits.
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public int Draw(ValueRange range) => (int)(range.Min + (long)(NextUInt64() % (ulong)((long)range.Max - range.Min + 1)));

    public bool Chance(double share) => NextDouble() < share;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
}

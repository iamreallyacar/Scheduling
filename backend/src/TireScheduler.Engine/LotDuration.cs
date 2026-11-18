namespace TireScheduler.Engine;

// The working time a lot needs (domain-model.md section 4, DEC-8). Each lot pays its own setup,
// and a cycle that is only partly filled still takes a whole cycle.
public static class LotDuration
{
    // Computed in 64 bits, so no combination of 32-bit inputs can overflow; the caller decides
    // whether the result fits what it stores.
    public static long Minutes(int setupMinutes, int cycleMinutes, int unitsPerCycle, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(setupMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitsPerCycle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        long cycles = ((long)quantity + unitsPerCycle - 1) / unitsPerCycle;
        return setupMinutes + (cycles * cycleMinutes);
    }
}

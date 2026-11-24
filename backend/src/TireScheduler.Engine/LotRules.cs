namespace TireScheduler.Engine;

// The parts of R8 that callers outside the engine also need, so the rule is written once.
public static class LotRules
{
    // Whether a place or move of the lot is not rejected by R8: it has not started and its order
    // is not cancelled (PlacementContext.LotViolations rejects the lot otherwise).
    public static bool CanChangePlacement(LotProgressState progress, bool orderIsCancelled) =>
        !HasStarted(progress) && !orderIsCancelled;

    internal static bool HasStarted(LotProgressState progress) => progress != LotProgressState.NotStarted;
}

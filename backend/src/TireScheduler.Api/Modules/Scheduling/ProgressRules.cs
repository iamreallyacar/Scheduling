namespace TireScheduler.Api.Modules.Scheduling;

// The two checks of a progress write, written once for the Progress module's rejections and the
// lot actions of Queries (DEC-70). In Scheduling, not the engine, because Progress may not use the
// engine (ModuleDependencyTests) and the live placement is this module's fact.
internal static class ProgressRules
{
    // The lot runs on its live placement's machine, and a cancelled order no longer changes (A47).
    public static bool CanRecordProgress(bool isPlacedLive, bool orderIsCancelled) => isPlacedLive && !orderIsCancelled;
}

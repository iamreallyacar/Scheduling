namespace TireScheduler.Engine;

// One-off calls of the rules in PlacementContext, each over a context built for that call. A
// caller that checks many requests against one snapshot builds the context once instead.
public static class PlacementRules
{
    public static DateTimeOffset? ComputeEnd(ScheduleSnapshot snapshot, int resourceId, DateTimeOffset plannedStart, int durationMinutes) =>
        new PlacementContext(snapshot).EndAfter(resourceId, plannedStart, durationMinutes);

    public static IReadOnlyList<RecomputedEnd> RecomputeEnds(ScheduleSnapshot snapshot) =>
        new PlacementContext(snapshot).RecomputeEnds();

    public static PlacementCheck Validate(ScheduleSnapshot snapshot, PlacementRequest request) =>
        new PlacementContext(snapshot).Validate(request);

    public static IReadOnlyList<RuleViolation> ValidateRemoval(ScheduleSnapshot snapshot, int lotId) =>
        new PlacementContext(snapshot).ValidateRemoval(lotId);

    public static IReadOnlyList<PlacementConflict> FindConflicts(ScheduleSnapshot snapshot) =>
        new PlacementContext(snapshot).FindConflicts();
}

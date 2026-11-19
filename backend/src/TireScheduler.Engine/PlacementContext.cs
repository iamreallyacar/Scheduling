namespace TireScheduler.Engine;

// Rules R1 to R4, R6 and R8 of domain-model.md section 5 over one indexed schedule version.
// Violations come in rule order, so the first is the one the API names in `rule` and `conflict`
// (AC-110). Built once, it checks any number of requests, as the earliest-slot search does per
// candidate; it holds no state between calls, so a check never counts an earlier request as placed.
public sealed class PlacementContext
{
    private readonly SnapshotIndex index;

    public PlacementContext(ScheduleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        index = new SnapshotIndex(snapshot);
    }

    // Null when the start is outside the resource's working time (R6).
    public DateTimeOffset? EndAfter(int resourceId, DateTimeOffset plannedStart, int durationMinutes) =>
        index.WorkingTimeOf(index.Resource(resourceId)).EndAfter(plannedStart, durationMinutes);

    // Places or moves a lot. The lot's own current placement is ignored (AC-81); nothing is adjusted (R7).
    public PlacementCheck Validate(PlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SnapshotLot lot = index.Lot(request.LotId);
        SnapshotResource resource = index.Resource(request.ResourceId);
        DateTimeOffset? plannedEnd = index.WorkingTimeOf(resource).EndAfter(request.PlannedStart, lot.DurationMinutes);

        List<RuleViolation> violations = RuleViolations(lot, resource, request.PlannedStart, plannedEnd, startIsWorkingTime: plannedEnd is not null);
        if (lot.Progress != LotProgressState.NotStarted)
        {
            violations.Add(LotViolation(RuleCodes.Started, lot));
        }

        if (lot.OrderIsCancelled)
        {
            violations.Add(LotViolation(RuleCodes.Cancelled, lot));
        }

        return new PlacementCheck(plannedEnd, violations);
    }

    // Removing a placement breaks no other rule (AC-99); only a started lot stays where it is (A17).
    internal IReadOnlyList<RuleViolation> ValidateRemoval(int lotId)
    {
        SnapshotLot lot = index.Lot(lotId);
        return lot.Progress == LotProgressState.NotStarted ? [] : [LotViolation(RuleCodes.Started, lot)];
    }

    // After a shift or holiday change (DEC-44): only the ends that change. A completed lot keeps the
    // end it ran to, and a start outside working time has no new end, so it keeps its stored one
    // and FindConflicts lists it under R6 (A42).
    internal IReadOnlyList<RecomputedEnd> RecomputeEnds()
    {
        List<RecomputedEnd> changedEnds = [];
        foreach (SnapshotPlacement placement in index.Placements)
        {
            SnapshotLot lot = index.Lot(placement.LotId);
            if (lot.Progress == LotProgressState.Completed)
            {
                continue;
            }

            DateTimeOffset? plannedEnd = index.WorkingTimeOf(index.Resource(placement.ResourceId)).EndAfter(placement.PlannedStart, lot.DurationMinutes);
            if (plannedEnd is { } newEnd && newEnd != placement.PlannedEnd)
            {
                changedEnds.Add(new RecomputedEnd(placement.Id, newEnd));
            }
        }

        return changedEnds;
    }

    // Placements that break a rule after a later fact (DEC-18), checked with their stored planned
    // ends. A completed lot is left out: its placement is the record of where it ran (A36).
    internal IReadOnlyList<PlacementConflict> FindConflicts()
    {
        List<PlacementConflict> conflicts = [];
        foreach (SnapshotPlacement placement in index.Placements.OrderBy(placement => placement.PlannedStart).ThenBy(placement => placement.Id))
        {
            SnapshotLot lot = index.Lot(placement.LotId);
            if (lot.Progress == LotProgressState.Completed)
            {
                continue;
            }

            SnapshotResource resource = index.Resource(placement.ResourceId);
            bool startIsWorkingTime = index.WorkingTimeOf(resource).Contains(placement.PlannedStart);
            List<RuleViolation> violations = RuleViolations(lot, resource, placement.PlannedStart, placement.PlannedEnd, startIsWorkingTime);
            if (violations.Count > 0)
            {
                conflicts.Add(new PlacementConflict(placement.Id, lot.Id, violations));
            }
        }

        return conflicts;
    }

    // R1 to R4 and R6 for the lot on the resource from start to end. Without an end (start outside
    // working time) the rules that need the span are not checked (A35).
    private List<RuleViolation> RuleViolations(
        SnapshotLot lot,
        SnapshotResource resource,
        DateTimeOffset start,
        DateTimeOffset? end,
        bool startIsWorkingTime)
    {
        List<RuleViolation> violations = [];
        if (resource.ResourceTypeId != lot.ResourceTypeId)
        {
            violations.Add(ResourceViolation(RuleCodes.ResourceType, resource));
        }

        if (!resource.IsActive)
        {
            violations.Add(ResourceViolation(RuleCodes.Inactive, resource));
        }

        if (end is { } spanEnd)
        {
            violations.AddRange(index.PlacementsOn(resource.Id)
                .Where(other => other.LotId != lot.Id && other.PlannedStart < spanEnd && start < other.PlannedEnd)
                .OrderBy(other => other.PlannedStart)
                .Select(other => new RuleViolation(RuleCodes.Overlap, new ConflictItem(ConflictKind.Placement, other.Id), EarliestAllowedStart: null)));

            // A16: the whole span counts, including non-working time in which the lot pauses.
            violations.AddRange(index.UnavailabilitiesOf(resource.Id)
                .Where(unavailability => unavailability.Start < spanEnd && (unavailability.End is not { } unavailabilityEnd || start < unavailabilityEnd))
                .OrderBy(unavailability => unavailability.Start)
                .Select(unavailability => new RuleViolation(RuleCodes.Unavailable, new ConflictItem(ConflictKind.Unavailability, unavailability.Id), EarliestAllowedStart: null)));
        }

        // Lag is elapsed time (A8). The predecessor that holds the start back the most comes first.
        violations.AddRange(index.PlacedPredecessors(lot.OperationId)
            .Select(link => (link.Placement, EarliestStart: link.Placement.PlannedEnd.AddMinutes(link.MinLagMinutes)))
            .Where(link => start < link.EarliestStart)
            .OrderByDescending(link => link.EarliestStart)
            .Select(link => new RuleViolation(RuleCodes.Dependency, new ConflictItem(ConflictKind.Lot, link.Placement.LotId), link.EarliestStart)));

        if (end is { } predecessorEnd)
        {
            violations.AddRange(index.PlacedSuccessors(lot.OperationId)
                .Where(link => link.Placement.PlannedStart < predecessorEnd.AddMinutes(link.MinLagMinutes))
                .OrderBy(link => link.Placement.PlannedStart)
                .Select(link => new RuleViolation(RuleCodes.Dependency, new ConflictItem(ConflictKind.Lot, link.Placement.LotId), EarliestAllowedStart: null)));
        }

        if (!startIsWorkingTime)
        {
            violations.Add(ResourceViolation(RuleCodes.OutsideWorkingTime, resource));
        }

        return violations;
    }

    private static RuleViolation ResourceViolation(string code, SnapshotResource resource) =>
        new(code, new ConflictItem(ConflictKind.Resource, resource.Id), EarliestAllowedStart: null);

    private static RuleViolation LotViolation(string code, SnapshotLot lot) =>
        new(code, new ConflictItem(ConflictKind.Lot, lot.Id), EarliestAllowedStart: null);
}

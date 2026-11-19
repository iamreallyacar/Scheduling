namespace TireScheduler.Engine;

// The earliest start that passes Validate on each candidate resource, and the best of them
// (engine README, section "Earliest slot search"). Only the points where a start can first become
// feasible are tried, and one context checks them all, so the search repeats no rule (DEC-47).
public static class EarliestSlot
{
    public static EarliestSlotResult Find(ScheduleSnapshot snapshot, EarliestSlotRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.Horizon, TimeSpan.Zero);
        SnapshotIndex index = new(snapshot);
        SnapshotLot lot = index.Lot(request.LotId);

        // Given resources are searched as given; one that is inactive or of another type gets no
        // slot, because Validate rejects every start on it (R1).
        SnapshotResource[] resources = request.ResourceIds is { } resourceIds
            ? [.. resourceIds.Select(index.Resource)]
            : [.. snapshot.Resources.Where(resource => resource.IsActive && resource.ResourceTypeId == lot.ResourceTypeId)];

        // R8 holds the lot back everywhere, which is a rejection rather than "no slot" (AC-124).
        IReadOnlyList<RuleViolation> lotViolations = PlacementContext.LotViolations(lot);
        if (lotViolations.Count > 0)
        {
            return new EarliestSlotResult(Slot: null, lotViolations);
        }

        PlacementContext context = new(index);
        DateTimeOffset searchStart = NextWholeMinute(request.NotBefore);
        DateTimeOffset searchEnd = searchStart + request.Horizon;

        List<(FoundSlot Slot, SnapshotResource Resource)> slots = [];
        foreach (SnapshotResource resource in resources)
        {
            if (EarliestOn(index, context, lot, resource, searchStart, searchEnd) is { } slot)
            {
                slots.Add((slot, resource));
            }
        }

        FoundSlot? best = slots
            .OrderBy(candidate => candidate.Slot.PlannedEnd)
            .ThenBy(candidate => candidate.Slot.PlannedStart)
            .ThenBy(candidate => candidate.Resource.Name, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Resource.Id)
            .Select(candidate => candidate.Slot)
            .FirstOrDefault();
        return new EarliestSlotResult(best, Violations: []);
    }

    // As start moves later the planned end never moves earlier, so a start that is infeasible
    // only because of an earlier item becomes feasible exactly when that item ends: at a working
    // period start, at the end of a placement or unavailability, or once the predecessors and
    // their lags allow it. Any feasible start is one of these points or searchStart itself.
    private static FoundSlot? EarliestOn(
        SnapshotIndex index,
        PlacementContext context,
        SnapshotLot lot,
        SnapshotResource resource,
        DateTimeOffset searchStart,
        DateTimeOffset searchEnd)
    {
        IEnumerable<DateTimeOffset> candidates = new[] { searchStart }
            .Concat(index.WorkingTimeOf(resource).PeriodsBetween(searchStart, searchEnd).Select(period => period.Start))
            .Concat(index.PlacementsOn(resource.Id).Select(placement => placement.PlannedEnd))
            .Concat(index.UnavailabilitiesOf(resource.Id).Select(unavailability => unavailability.End).OfType<DateTimeOffset>())
            .Concat(index.PlacedPredecessors(lot.OperationId).Select(link => link.Placement.PlannedEnd.AddMinutes(link.MinLagMinutes)))
            .Select(NextWholeMinute)
            .Where(start => searchStart <= start && start < searchEnd)
            .Distinct()
            .Order();
        foreach (DateTimeOffset start in candidates)
        {
            PlacementCheck check = context.Validate(new PlacementRequest(lot.Id, resource.Id, start));
            if (check.IsAccepted)
            {
                return new FoundSlot(resource.Id, start, check.PlannedEnd ?? throw new InvalidOperationException("An accepted placement check has no planned end."));
            }
        }

        return null;
    }

    // A placement starts on a whole minute (A40, A45).
    private static DateTimeOffset NextWholeMinute(DateTimeOffset instant)
    {
        long ticksIntoMinute = instant.UtcTicks % TimeSpan.TicksPerMinute;
        return ticksIntoMinute == 0 ? instant : instant.AddTicks(TimeSpan.TicksPerMinute - ticksIntoMinute);
    }
}

// No resources means every active resource of the lot's type. The horizon is the caller's
// setting (A45).
public sealed record EarliestSlotRequest(int LotId, IReadOnlyList<int>? ResourceIds, DateTimeOffset NotBefore, TimeSpan Horizon);

public sealed record FoundSlot(int ResourceId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd);

// Violations are the R8 rejections of the lot itself (AC-124). Without them, a missing slot means
// that nothing fits within the horizon (slot.none).
public sealed record EarliestSlotResult(FoundSlot? Slot, IReadOnlyList<RuleViolation> Violations);

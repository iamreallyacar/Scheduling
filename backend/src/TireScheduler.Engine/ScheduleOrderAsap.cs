namespace TireScheduler.Engine;

// F-SCH-16: the earliest slot of every unplaced lot of one order, in dependency order, each found
// with the earlier proposals counted as placed (engine README, "Operations"). Existing placements
// never move, and the proposal holds every lot or none (DEC-19).
public static class ScheduleOrderAsap
{
    public static OrderProposal Propose(ScheduleSnapshot snapshot, OrderProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.Horizon, TimeSpan.Zero);
        SnapshotIndex index = new(snapshot);
        SnapshotLot[] lots = [.. request.LotIds.Select(index.Lot)];
        // A lot given twice would be proposed twice, because a search ignores the lot's own placement.
        if (lots.DistinctBy(lot => lot.Id).Count() != lots.Length)
        {
            throw new ArgumentException("A lot is given more than once.", nameof(request));
        }

        // A cancelled order is rejected as a whole, even when nothing is left to place (AC-133).
        if (lots.FirstOrDefault(lot => lot.OrderIsCancelled) is { } lotOfCancelledOrder)
        {
            return new OrderProposal(Placements: [], LotWithoutSlot: null, PlacementContext.LotViolations(lotOfCancelledOrder));
        }

        HashSet<int> placedLotIds = [.. snapshot.Placements.Select(placement => placement.LotId)];
        List<ProposedPlacement> proposed = [];
        ScheduleSnapshot withProposals = snapshot;
        foreach (SnapshotLot lot in InDependencyOrder(lots, snapshot.Dependencies).Where(lot => !placedLotIds.Contains(lot.Id)))
        {
            // A PlacementContext never counts an earlier request as placed, so each search gets
            // a snapshot that holds the proposals so far (DEC-47).
            EarliestSlotResult result = EarliestSlot.Find(withProposals, new EarliestSlotRequest(lot.Id, ResourceIds: null, request.NotBefore, request.Horizon));
            if (result.Violations.Count > 0)
            {
                return new OrderProposal(Placements: [], LotWithoutSlot: null, result.Violations);
            }

            if (result.Slot is not { } slot)
            {
                return new OrderProposal(Placements: [], lot.Id, Violations: []);
            }

            proposed.Add(new ProposedPlacement(lot.Id, slot.ResourceId, slot.PlannedStart, slot.PlannedEnd));
            // Ids below zero never meet a stored placement's; they exist only inside this search.
            withProposals = withProposals with
            {
                Placements = [.. withProposals.Placements, new SnapshotPlacement(-proposed.Count, lot.Id, slot.ResourceId, slot.PlannedStart, slot.PlannedEnd)],
            };
        }

        return new OrderProposal(proposed, LotWithoutSlot: null, Violations: []);
    }

    // A lot comes once no lot of an operation it waits for is still waiting. Among the lots that
    // may come next, the lower sequence number goes first, then the lower lot id (AC-129). A
    // routing may link a stage to one with a lower number, so the numbers alone are no order.
    private static List<SnapshotLot> InDependencyOrder(IReadOnlyList<SnapshotLot> lots, IReadOnlyList<StageDependency> dependencies)
    {
        HashSet<int> operationIds = [.. lots.Select(lot => lot.OperationId)];
        StageDependency[] links = [.. dependencies.Where(link => operationIds.Contains(link.PredecessorOperationId) && operationIds.Contains(link.SuccessorOperationId))];
        List<SnapshotLot> waiting = [.. lots];
        List<SnapshotLot> ordered = [];
        while (waiting.Count > 0)
        {
            SnapshotLot next = waiting
                .Where(lot => links.All(link => link.SuccessorOperationId != lot.OperationId || waiting.All(other => other.OperationId != link.PredecessorOperationId)))
                .OrderBy(lot => lot.SequenceNo)
                .ThenBy(lot => lot.Id)
                .FirstOrDefault() ?? throw new ArgumentException("The dependencies between the lots form a cycle.", nameof(dependencies));
            waiting.Remove(next);
            ordered.Add(next);
        }

        return ordered;
    }
}

// The lots of one order, placed or not; the placed ones stay where they are. The horizon is the
// caller's setting, as for EarliestSlot (A45).
public sealed record OrderProposalRequest(IReadOnlyList<int> LotIds, DateTimeOffset NotBefore, TimeSpan Horizon);

public sealed record ProposedPlacement(int LotId, int ResourceId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd);

// The placements in the order they were proposed. LotWithoutSlot names the first lot that has no
// slot within the horizon (AC-131), and Violations hold the R8 rejection (AC-133); with either
// there is no proposal.
public sealed record OrderProposal(IReadOnlyList<ProposedPlacement> Placements, int? LotWithoutSlot, IReadOnlyList<RuleViolation> Violations);

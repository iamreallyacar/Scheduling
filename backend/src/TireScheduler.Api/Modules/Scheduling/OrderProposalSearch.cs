using Microsoft.Extensions.Options;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// F-SCH-16: the as-soon-as-possible proposal of a whole order (DEC-60). It writes nothing, so it
// takes no locks; the commit makes the proposal again under its locks (PlacementRegister).
internal sealed class OrderProposalSearch(
    ScheduleVersions versions,
    ScheduleSnapshotLoader snapshots,
    IOrderReader orders,
    IOptions<SchedulingOptions> options,
    TimeProvider clock)
{
    public async Task<OrderProposalResponse> ProposeAsync(string versionPath, int orderId, DateTimeOffset? notBefore, CancellationToken cancellationToken)
    {
        int versionId = await versions.ResolveAsync(versionPath, cancellationToken);
        OrderDetails details = await orders.GetAsync(orderId, cancellationToken);
        OrderSchedulingFacts order = await orders.FindSchedulingFactsAsync(orderId, cancellationToken) ?? throw ApiProblemException.NotFound("This order");
        DateTimeOffset searchStart = (notBefore ?? clock.GetUtcNow()).ToUniversalTime();
        ScheduleSnapshot snapshot = await snapshots.ForOrderAsync(versionId, order, cancellationToken);
        OrderProposal proposal = Propose(snapshot, order, searchStart);
        if (proposal.Violations.Count > 0)
        {
            throw RuleViolationProblems.Rejection(proposal.Violations);
        }

        if (proposal.LotWithoutSlot is { } lotWithoutSlot)
        {
            throw NoSlot(details, lotWithoutSlot);
        }

        IReadOnlyList<ProposalLotResponse> lots = LotsOf(details, snapshot, proposal);
        return new OrderProposalResponse
        {
            OrderId = orderId,
            NotBefore = searchStart,
            Lots = lots,
            // Every order has a lot, because an order needs a product with a routing stage.
            FinishesAt = lots.Max(lot => lot.PlannedEnd),
            // The engine's late flag rule, which Queries uses for stored placements (DEC-30). Until
            // task M5-1 no lot is completed, so there is no completion time, as in OrderLateRules.
            WouldBeLate = OrderLateness.IsLate(new LatenessFacts(
                details.Order.DueDate,
                snapshot.PlantTimeZoneId,
                clock.GetUtcNow(),
                IsCancelled: details.Order.CancelledAt is not null,
                PlannedEnds: [.. lots.Select(lot => lot.PlannedEnd)],
                CompletedAt: null)),
        };
    }

    // Also the commit's check: the engine is deterministic, so the same snapshot and start give the
    // same proposal (DEC-60). Every lot of the order is passed; the placed ones stay (AC-130).
    public OrderProposal Propose(ScheduleSnapshot snapshot, OrderSchedulingFacts order, DateTimeOffset notBefore) =>
        ScheduleOrderAsap.Propose(snapshot, new OrderProposalRequest(
            [.. order.Lots.Select(lot => lot.Id)],
            notBefore,
            TimeSpan.FromDays(options.Value.SearchHorizonDays)));

    // The order was read twice, so its lots may have been generated again in between; then the
    // planner asks again.
    private static IReadOnlyList<ProposalLotResponse> LotsOf(OrderDetails details, ScheduleSnapshot snapshot, OrderProposal proposal)
    {
        Dictionary<int, ProposedPlacement> proposedByLot = proposal.Placements.ToDictionary(placement => placement.LotId);
        Dictionary<int, SnapshotPlacement> placedByLot = snapshot.Placements.ToDictionary(placement => placement.LotId);
        Dictionary<int, SnapshotResource> resourcesById = snapshot.Resources.ToDictionary(resource => resource.Id);
        return
        [
            .. details.Operations.OrderBy(operation => operation.SequenceNo).SelectMany(operation => operation.Lots.OrderBy(lot => lot.LotNo).Select(lot =>
            {
                (int resourceId, DateTimeOffset plannedStart, DateTimeOffset plannedEnd, bool isProposed) =
                    proposedByLot.TryGetValue(lot.Id, out ProposedPlacement? proposed) ? (proposed.ResourceId, proposed.PlannedStart, proposed.PlannedEnd, true)
                    : placedByLot.TryGetValue(lot.Id, out SnapshotPlacement? placed) ? (placed.ResourceId, placed.PlannedStart, placed.PlannedEnd, false)
                    : throw Stale();
                return new ProposalLotResponse
                {
                    LotId = lot.Id,
                    LotNo = lot.LotNo,
                    SequenceNo = operation.SequenceNo,
                    ResourceType = operation.ResourceType,
                    Resource = new NamedReference { Id = resourceId, Name = resourcesById[resourceId].Name },
                    PlannedStart = plannedStart,
                    PlannedEnd = plannedEnd,
                    IsProposed = isProposed,
                };
            })),
        ];
    }

    // Also the commit's answer when its body is no longer what the engine proposes (AC-135).
    public static ApiProblemException Stale() => ApiProblemException.Rejected(
        StatusCodes.Status409Conflict,
        ProblemCodes.ProposalStale,
        "The schedule or the order changed since this proposal was made, so nothing was saved. Ask for a proposal again.");

    private ApiProblemException NoSlot(OrderDetails details, int lotId)
    {
        OperationDetails operation = details.Operations.Single(candidate => candidate.Lots.Any(lot => lot.Id == lotId));
        LotDetails lot = operation.Lots.Single(candidate => candidate.Id == lotId);
        return ApiProblemException.Rejected(
            StatusCodes.Status422UnprocessableEntity,
            ProblemCodes.SlotNone,
            $"Lot {lot.LotNo} of stage {operation.SequenceNo} ({operation.ResourceType.Name}) has no free slot within {options.Value.SearchHorizonDays} days after the search start, so nothing is proposed. Check breakdowns and inactive machines.",
            new Dictionary<string, object> { ["lotId"] = lotId });
    }
}

using Microsoft.Extensions.Options;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// F-SCH-11: the earliest slot of a lot, as a suggestion the planner then places (AC-126). It
// writes nothing, so it takes no locks; the placement checks every rule again when it is saved.
internal sealed class EarliestSlotSearch(
    ScheduleVersions versions,
    ScheduleSnapshotLoader snapshots,
    IOrderReader orders,
    IOptions<SchedulingOptions> options,
    TimeProvider clock)
{
    public async Task<EarliestSlotResponse> FindAsync(string versionPath, int lotId, int? resourceId, DateTimeOffset? notBefore, CancellationToken cancellationToken)
    {
        int versionId = await versions.ResolveAsync(versionPath, cancellationToken);
        int orderId = await orders.FindOrderIdOfLotAsync(lotId, cancellationToken) ?? throw LotNotFound();
        OrderSchedulingFacts order = await orders.FindSchedulingFactsAsync(orderId, cancellationToken) ?? throw LotNotFound();
        // The lots of an order are generated again when it changes, so the lot may be gone by now.
        SchedulingLotFacts lot = order.Lots.SingleOrDefault(orderLot => orderLot.Id == lotId) ?? throw LotNotFound();
        ScheduleSnapshot snapshot = await snapshots.ForOrderOnResourceTypeAsync(versionId, order, lot.ResourceTypeId, cancellationToken);

        // The engine searches a given machine as given (AC-195); one it does not know is a wrong
        // reference, not a machine without a slot.
        if (resourceId is { } givenId && snapshot.Resources.All(resource => resource.Id != givenId))
        {
            throw ApiProblemException.ValidationFailed("resourceId", "This machine does not exist.");
        }

        int horizonDays = options.Value.SearchHorizonDays;
        EarliestSlotResult result = EarliestSlot.Find(snapshot, new EarliestSlotRequest(
            lotId,
            resourceId is { } searchedId ? [searchedId] : null,
            (notBefore ?? clock.GetUtcNow()).ToUniversalTime(),
            TimeSpan.FromDays(horizonDays)));
        if (result.Violations.Count > 0)
        {
            throw RuleViolationProblems.Rejection(result.Violations);
        }

        FoundSlot slot = result.Slot ?? throw ApiProblemException.Rejected(
            StatusCodes.Status422UnprocessableEntity,
            ProblemCodes.SlotNone,
            $"No machine that was searched has a free slot for this lot within {horizonDays} days after the search start. Check breakdowns and inactive machines.");
        SnapshotResource slotResource = snapshot.Resources.Single(resource => resource.Id == slot.ResourceId);
        return new EarliestSlotResponse
        {
            LotId = lotId,
            Resource = new NamedReference { Id = slotResource.Id, Name = slotResource.Name },
            PlannedStart = slot.PlannedStart,
            PlannedEnd = slot.PlannedEnd,
        };
    }

    private static ApiProblemException LotNotFound() => ApiProblemException.NotFound("This lot");
}

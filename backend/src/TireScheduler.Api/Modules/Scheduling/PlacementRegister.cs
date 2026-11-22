using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// F-SCH-1: place, move and remove a lot in one version. R3 and R4 span modules, so a placement
// locks the resource row and then the order row, builds the snapshot, validates and writes in
// one transaction (ARCHITECTURE.md section "Persistence rules"). Every command that takes both
// locks takes them in that order, so two of them never wait on each other. It is the only writer
// of placements, also for a committed whole-order proposal.
internal sealed class PlacementRegister(
    SchedulingDbContext context,
    ScheduleVersions versions,
    ScheduleSnapshotLoader snapshots,
    IOrderReader orders,
    IOrderLocks orderLocks,
    IMasterDataLocks masterDataLocks,
    OrderProposalSearch proposals,
    RequestTransaction requestTransaction)
{
    public async Task<LotPlacementResponse> PlaceAsync(string versionPath, int lotId, LotPlacementRequest request, CancellationToken cancellationToken)
    {
        int resourceId = request.ResourceId.Validated();
        DateTimeOffset plannedStart = request.PlannedStart.Validated().ToUniversalTime();
        if (WholeMinuteTime.ErrorOf(plannedStart, "start") is { } startError)
        {
            throw ApiProblemException.ValidationFailed("plannedStart", startError);
        }

        int versionId = await versions.ResolveAsync(versionPath, cancellationToken);
        int orderId = await orders.FindOrderIdOfLotAsync(lotId, cancellationToken) ?? throw LotNotFound();

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        if (!await masterDataLocks.LockResourceAsync(resourceId, cancellationToken))
        {
            throw ApiProblemException.ValidationFailed("resourceId", "This machine does not exist.");
        }

        OrderSchedulingFacts order = await LockOrderOfLotAsync(orderId, lotId, cancellationToken);
        ScheduleSnapshot snapshot = await snapshots.ForOrderOnResourceAsync(versionId, order, resourceId, cancellationToken);
        PlacementCheck check = PlacementRules.Validate(snapshot, new PlacementRequest(lotId, resourceId, plannedStart));
        if (!check.IsAccepted)
        {
            throw RuleViolationProblems.Rejection(check.Violations);
        }

        Placement? placement = await FindAsync(versionId, lotId, cancellationToken);
        if (placement is null)
        {
            placement = new Placement { VersionId = versionId, LotId = lotId };
            context.Placements.Add(placement);
        }

        placement.ResourceId = resourceId;
        placement.PlannedStart = plannedStart;
        placement.PlannedEnd = check.PlannedEnd ?? throw new InvalidOperationException("The engine accepted a placement without a planned end.");
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LotPlacementResponse
        {
            LotId = lotId,
            ResourceId = placement.ResourceId,
            PlannedStart = placement.PlannedStart,
            PlannedEnd = placement.PlannedEnd,
        };
    }

    // F-SCH-16: saves a whole-order proposal only while it is what the engine proposes from the same
    // start (DEC-60). The search may use any resource of the order's lot types, so all of them are
    // locked, in ascending id order as WorkingTimeChange does, and then the order, as a placement does.
    public async Task<IReadOnlyList<LotPlacementResponse>> CommitProposalAsync(string versionPath, int orderId, OrderProposalCommitRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset notBefore = request.NotBefore.Validated().ToUniversalTime();
        (int LotId, int ResourceId, DateTimeOffset PlannedStart)[] sent = [.. request.Placements.Validated().Select(placement =>
            (placement.LotId.Validated(), placement.ResourceId.Validated(), placement.PlannedStart.Validated().ToUniversalTime()))];
        int versionId = await versions.ResolveAsync(versionPath, cancellationToken);
        // Read before the locks only to check the body and to know which resources to lock; the
        // order is read again under its lock, because its lots may change meanwhile.
        OrderSchedulingFacts orderBeforeLocks = await orders.FindSchedulingFactsAsync(orderId, cancellationToken) ?? throw OrderNotFound();
        ThrowOnUnfitPlacements(sent, orderBeforeLocks);

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        HashSet<int> lockedResourceIds = [];
        foreach (int resourceId in await snapshots.ResourceIdsOfLotTypesAsync(orderBeforeLocks, cancellationToken))
        {
            if (await masterDataLocks.LockResourceAsync(resourceId, cancellationToken))
            {
                lockedResourceIds.Add(resourceId);
            }
        }

        OrderSchedulingFacts order = await orderLocks.LockOrderAsync(orderId, cancellationToken)
            ? await orders.FindSchedulingFactsAsync(orderId, cancellationToken) ?? throw OrderNotFound()
            : throw OrderNotFound();
        ScheduleSnapshot snapshot = await snapshots.ForOrderAsync(versionId, order, cancellationToken);
        HashSet<int> lotTypeIds = [.. order.Lots.Select(lot => lot.ResourceTypeId)];
        // A resource added or an order changed since the locks were taken would let the search use
        // a resource that another command may write meanwhile.
        if (snapshot.Resources.Any(resource => lotTypeIds.Contains(resource.ResourceTypeId) && !lockedResourceIds.Contains(resource.Id)))
        {
            throw OrderProposalSearch.Stale();
        }

        OrderProposal proposal = proposals.Propose(snapshot, order, notBefore);
        if (proposal.Violations.Count > 0)
        {
            throw RuleViolationProblems.Rejection(proposal.Violations);
        }

        HashSet<(int LotId, int ResourceId, DateTimeOffset PlannedStart)> proposed = [.. proposal.Placements.Select(placement => (placement.LotId, placement.ResourceId, placement.PlannedStart))];
        if (proposal.LotWithoutSlot is not null || !proposed.SetEquals(sent) || proposed.Count != sent.Length)
        {
            throw OrderProposalSearch.Stale();
        }

        List<Placement> saved = [.. proposal.Placements.Select(placement => new Placement
        {
            VersionId = versionId,
            LotId = placement.LotId,
            ResourceId = placement.ResourceId,
            PlannedStart = placement.PlannedStart,
            PlannedEnd = placement.PlannedEnd,
        })];
        context.Placements.AddRange(saved);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return [.. saved.Select(placement => new LotPlacementResponse
        {
            LotId = placement.LotId,
            ResourceId = placement.ResourceId,
            PlannedStart = placement.PlannedStart,
            PlannedEnd = placement.PlannedEnd,
        })];
    }

    // What the body itself gets wrong is a field error (AC-136, AC-200); what the schedule changed is stale.
    private static void ThrowOnUnfitPlacements((int LotId, int ResourceId, DateTimeOffset PlannedStart)[] sent, OrderSchedulingFacts order)
    {
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        HashSet<int> seenLotIds = [];
        for (int position = 0; position < sent.Length; position++)
        {
            if (WholeMinuteTime.ErrorOf(sent[position].PlannedStart, "start") is { } startError)
            {
                fieldErrors[$"placements[{position}].plannedStart"] = [startError];
            }

            if (order.Lots.All(lot => lot.Id != sent[position].LotId))
            {
                fieldErrors[$"placements[{position}].lotId"] = ["This lot is not a lot of this order."];
            }
            else if (!seenLotIds.Add(sent[position].LotId))
            {
                fieldErrors[$"placements[{position}].lotId"] = ["This lot is given more than once."];
            }
        }

        if (fieldErrors.Count > 0)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }
    }

    private static ApiProblemException OrderNotFound() => ApiProblemException.NotFound("This order");

    // Removing breaks no rule except R8 (AC-99), so only the order is locked.
    public async Task RemoveAsync(string versionPath, int lotId, CancellationToken cancellationToken)
    {
        int versionId = await versions.ResolveAsync(versionPath, cancellationToken);
        int orderId = await orders.FindOrderIdOfLotAsync(lotId, cancellationToken) ?? throw LotNotFound();

        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        OrderSchedulingFacts order = await LockOrderOfLotAsync(orderId, lotId, cancellationToken);
        Placement placement = await FindAsync(versionId, lotId, cancellationToken)
            ?? throw ApiProblemException.NotFound("A placement of this lot in this version");
        ScheduleSnapshot snapshot = await snapshots.ForOrderOnResourceAsync(versionId, order, placement.ResourceId, cancellationToken);
        IReadOnlyList<RuleViolation> violations = PlacementRules.ValidateRemoval(snapshot, lotId);
        if (violations.Count > 0)
        {
            throw RuleViolationProblems.Rejection(violations);
        }

        context.Placements.Remove(placement);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Read again under the lock: the order may have been deleted, or its lots generated again,
    // since the lot was looked up.
    private async Task<OrderSchedulingFacts> LockOrderOfLotAsync(int orderId, int lotId, CancellationToken cancellationToken)
    {
        OrderSchedulingFacts? order = await orderLocks.LockOrderAsync(orderId, cancellationToken)
            ? await orders.FindSchedulingFactsAsync(orderId, cancellationToken)
            : null;
        return order is not null && order.Lots.Any(lot => lot.Id == lotId) ? order : throw LotNotFound();
    }

    private Task<Placement?> FindAsync(int versionId, int lotId, CancellationToken cancellationToken) =>
        context.Placements.SingleOrDefaultAsync(placement => placement.VersionId == versionId && placement.LotId == lotId, cancellationToken);

    private static ApiProblemException LotNotFound() => ApiProblemException.NotFound("This lot");
}

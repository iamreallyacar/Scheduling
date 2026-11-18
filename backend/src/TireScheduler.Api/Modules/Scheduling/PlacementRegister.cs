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
// locks takes them in that order, so two of them never wait on each other.
internal sealed class PlacementRegister(
    SchedulingDbContext context,
    ScheduleVersions versions,
    ScheduleSnapshotLoader snapshots,
    IOrderReader orders,
    IOrderLocks orderLocks,
    IMasterDataLocks masterDataLocks,
    RequestTransaction requestTransaction)
{
    public async Task<LotPlacementResponse> PlaceAsync(string versionPath, int lotId, LotPlacementRequest request, CancellationToken cancellationToken)
    {
        int resourceId = request.ResourceId.Validated();
        DateTimeOffset plannedStart = request.PlannedStart.Validated().ToUniversalTime();
        // Times are whole minutes (A2, A40).
        if (plannedStart.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw ApiProblemException.ValidationFailed("plannedStart", "The start must be a whole minute.");
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

using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Machines;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Engine;

namespace TireScheduler.Api.Modules.Scheduling;

// Builds the engine snapshot of one version from the owning modules (ARCHITECTURE.md section
// "Data flow"). Only the engine converts plant-local shift times (ARCHITECTURE.md section "Time rules").
internal sealed class ScheduleSnapshotLoader(
    SchedulingDbContext context,
    IMasterDataReader masterData,
    EngineWorkingTimeLoader workingTimeLoader,
    IOrderReader orders,
    ILotProgressStates lotProgress)
{
    // Enough to check a lot of the order on the resource.
    public Task<ScheduleSnapshot> ForOrderOnResourceAsync(int versionId, OrderSchedulingFacts order, int resourceId, CancellationToken cancellationToken) =>
        LoadAsync(versionId, new OrdersOnResources { ResourceIds = [resourceId], Orders = [order] }, cancellationToken);

    // Enough to search a lot of the order on every resource of its type (EarliestSlot). Resources
    // are loaded whole, so a given resource of another type is in the snapshot without its
    // placements and unavailabilities, which no check reads, because R1 rejects every start on it.
    public async Task<ScheduleSnapshot> ForOrderOnResourceTypeAsync(int versionId, OrderSchedulingFacts order, int resourceTypeId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ResourceResponse> resourcesOfType = await masterData.ListResourcesAsync(resourceTypeId, isActive: null, cancellationToken);
        return await LoadAsync(versionId, new OrdersOnResources { ResourceIds = [.. resourcesOfType.Select(resource => resource.Id)], Orders = [order] }, cancellationToken);
    }

    // Every placement of the version with the orders of their lots, for the rules that look at the
    // whole version (FindConflicts, RecomputeEnds).
    public Task<ScheduleSnapshot> ForVersionAsync(int versionId, CancellationToken cancellationToken) =>
        LoadAsync(versionId, scope: null, cancellationToken);

    // The rows each rule reads, so a scope that leaves one out is visible here and not only as a
    // rule that quietly sees less: R2 the placements on the scoped resources and R3 their
    // unavailabilities; R4 the lots, dependencies and placements of the scoped orders, whose
    // dependencies link only their own lots (A11); R8 the progress of those lots; R1 and R6 every
    // resource and calendar, which are always loaded whole. A scope holds placements of other
    // orders' lots without those lots, so it serves only checks of its own orders' lots (Validate,
    // ValidateRemoval); the rules over every placement get the whole version (scope null), whose
    // orders come from the same placement rows, so every placed lot is in the snapshot.
    private async Task<ScheduleSnapshot> LoadAsync(int versionId, OrdersOnResources? scope, CancellationToken cancellationToken)
    {
        IQueryable<Placement> placements = context.Placements.Where(placement => placement.VersionId == versionId);
        IQueryable<Unavailability> unavailabilities = context.Unavailabilities;
        if (scope is not null)
        {
            int[] lotIdsOfScope = scope.Orders.SelectMany(order => order.Lots).Select(lot => lot.Id).ToArray();
            placements = placements.Where(placement => scope.ResourceIds.Contains(placement.ResourceId) || lotIdsOfScope.Contains(placement.LotId));
            unavailabilities = unavailabilities.Where(unavailability => scope.ResourceIds.Contains(unavailability.ResourceId));
        }

        List<SnapshotPlacement> snapshotPlacements = await placements
            .Select(placement => new SnapshotPlacement(placement.Id, placement.LotId, placement.ResourceId, placement.PlannedStart, placement.PlannedEnd))
            .ToListAsync(cancellationToken);
        IReadOnlyList<OrderSchedulingFacts> ordersInSnapshot = scope?.Orders
            ?? await orders.ListSchedulingFactsOfLotsAsync(snapshotPlacements.Select(placement => placement.LotId).ToArray(), cancellationToken);
        int[] lotIdsInSnapshot = ordersInSnapshot.SelectMany(order => order.Lots).Select(lot => lot.Id).ToArray();
        List<SnapshotUnavailability> snapshotUnavailabilities = await unavailabilities
            .Select(unavailability => new SnapshotUnavailability(unavailability.Id, unavailability.ResourceId, unavailability.StartsAt, unavailability.EndsAt))
            .ToListAsync(cancellationToken);
        IReadOnlyDictionary<int, StartedLotState> startedLots = await lotProgress.StartedLotsAsync(lotIdsInSnapshot, cancellationToken);
        EngineWorkingTime workingTime = await workingTimeLoader.LoadAsync(cancellationToken);
        IReadOnlyList<ResourceResponse> resources = await masterData.ListResourcesAsync(resourceTypeId: null, isActive: null, cancellationToken);

        return new ScheduleSnapshot(
            workingTime.TimeZoneId,
            workingTime.Calendars,
            workingTime.Holidays,
            [.. resources.Select(resource => new SnapshotResource(resource.Id, resource.Name, resource.ResourceType.Id, resource.Calendar.Id, resource.IsActive))],
            snapshotUnavailabilities,
            [.. ordersInSnapshot.SelectMany(order => order.Lots.Select(lot => new SnapshotLot(
                lot.Id,
                lot.OperationId,
                lot.DurationMinutes,
                lot.ResourceTypeId,
                ProgressOf(startedLots, lot.Id),
                order.IsCancelled)))],
            [.. ordersInSnapshot.SelectMany(order => order.Dependencies).Select(dependency => new StageDependency(dependency.PredecessorOperationId, dependency.SuccessorOperationId, dependency.MinLagMinutes))],
            snapshotPlacements);
    }

    private static LotProgressState ProgressOf(IReadOnlyDictionary<int, StartedLotState> startedLots, int lotId) =>
        !startedLots.TryGetValue(lotId, out StartedLotState state) ? LotProgressState.NotStarted
        : state == StartedLotState.Completed ? LotProgressState.Completed
        : LotProgressState.InProgress;

    // The orders are passed whole, not as ids, because a placement command has already read them
    // under the order lock.
    private sealed record OrdersOnResources
    {
        public required IReadOnlyCollection<int> ResourceIds { get; init; }

        public required IReadOnlyList<OrderSchedulingFacts> Orders { get; init; }
    }
}

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
    IOrderReader orders,
    ILotProgressStates lotProgress)
{
    // Enough to check a lot of the order on the resource: R4 compares the order's lots, which are
    // the only ones its dependencies link (A11); R2 and R3 look only at the resource.
    public async Task<ScheduleSnapshot> ForOrderOnResourceAsync(int versionId, OrderSchedulingFacts order, int resourceId, CancellationToken cancellationToken)
    {
        int[] lotIds = order.Lots.Select(lot => lot.Id).ToArray();
        List<SnapshotPlacement> placements = await context.Placements
            .Where(placement => placement.VersionId == versionId && (placement.ResourceId == resourceId || lotIds.Contains(placement.LotId)))
            .Select(placement => new SnapshotPlacement(placement.Id, placement.LotId, placement.ResourceId, placement.PlannedStart, placement.PlannedEnd))
            .ToListAsync(cancellationToken);
        List<SnapshotUnavailability> unavailabilities = await context.Unavailabilities
            .Where(unavailability => unavailability.ResourceId == resourceId)
            .Select(unavailability => new SnapshotUnavailability(unavailability.Id, unavailability.ResourceId, unavailability.StartsAt, unavailability.EndsAt))
            .ToListAsync(cancellationToken);
        return await BuildAsync([order], placements, unavailabilities, cancellationToken);
    }

    // Every placement of the version with the orders of their lots, for the rules that look at the
    // whole version (FindConflicts, RecomputeEnds).
    public async Task<ScheduleSnapshot> ForVersionAsync(int versionId, CancellationToken cancellationToken)
    {
        List<SnapshotPlacement> placements = await context.Placements
            .Where(placement => placement.VersionId == versionId)
            .Select(placement => new SnapshotPlacement(placement.Id, placement.LotId, placement.ResourceId, placement.PlannedStart, placement.PlannedEnd))
            .ToListAsync(cancellationToken);
        IReadOnlyList<OrderSchedulingFacts> ordersOfPlacedLots = await orders.ListSchedulingFactsOfLotsAsync(
            placements.Select(placement => placement.LotId).ToArray(),
            cancellationToken);
        List<SnapshotUnavailability> unavailabilities = await context.Unavailabilities
            .Select(unavailability => new SnapshotUnavailability(unavailability.Id, unavailability.ResourceId, unavailability.StartsAt, unavailability.EndsAt))
            .ToListAsync(cancellationToken);
        return await BuildAsync(ordersOfPlacedLots, placements, unavailabilities, cancellationToken);
    }

    private async Task<ScheduleSnapshot> BuildAsync(
        IReadOnlyList<OrderSchedulingFacts> ordersInSnapshot,
        List<SnapshotPlacement> placements,
        List<SnapshotUnavailability> unavailabilities,
        CancellationToken cancellationToken)
    {
        int[] lotIds = ordersInSnapshot.SelectMany(order => order.Lots).Select(lot => lot.Id).ToArray();
        IReadOnlyDictionary<int, StartedLotState> startedLots = await lotProgress.StartedLotsAsync(lotIds, cancellationToken);
        PlantWorkingTime workingTime = await masterData.GetWorkingTimeAsync(cancellationToken);
        IReadOnlyList<ResourceResponse> resources = await masterData.ListResourcesAsync(resourceTypeId: null, isActive: null, cancellationToken);

        return new ScheduleSnapshot(
            masterData.PlantTimeZoneId,
            [.. workingTime.Calendars.Select(calendar => new SnapshotCalendar(
                calendar.Id,
                [.. calendar.Shifts.Select(shift => new SnapshotShift(shift.Weekday, shift.StartMinuteOfDay, shift.EndMinuteOfDay))]))],
            workingTime.Holidays.ToHashSet(),
            [.. resources.Select(resource => new SnapshotResource(resource.Id, resource.ResourceType.Id, resource.Calendar.Id, resource.IsActive))],
            unavailabilities,
            [.. ordersInSnapshot.SelectMany(order => order.Lots.Select(lot => new SnapshotLot(
                lot.Id,
                lot.OperationId,
                lot.DurationMinutes,
                lot.ResourceTypeId,
                ProgressOf(startedLots, lot.Id),
                order.IsCancelled)))],
            [.. ordersInSnapshot.SelectMany(order => order.Dependencies).Select(dependency => new StageDependency(dependency.PredecessorOperationId, dependency.SuccessorOperationId, dependency.MinLagMinutes))],
            placements);
    }

    private static LotProgressState ProgressOf(IReadOnlyDictionary<int, StartedLotState> startedLots, int lotId) =>
        !startedLots.TryGetValue(lotId, out StartedLotState state) ? LotProgressState.NotStarted
        : state == StartedLotState.Completed ? LotProgressState.Completed
        : LotProgressState.InProgress;
}

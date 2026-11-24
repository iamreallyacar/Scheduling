using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries.Machines;
using TireScheduler.Api.Queries.Orders;
using TireScheduler.Api.Queries.Schedule;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Dashboard;

// The planner dashboard (DEC-65), put together from the readers that already answer each part, so
// each rule stays in one place.
internal sealed class DashboardReader(
    UnscheduledLotReader unscheduledLots,
    IPlacementConflictReader placementConflicts,
    OrderViewReader orderViews,
    MachineViewReader machineViews,
    IMasterDataReader masterData,
    TimeProvider clock)
{
    // The week ahead after today (A49).
    public const int DueSoonDays = 7;

    public async Task<DashboardResponse> GetAsync(string versionId, CancellationToken cancellationToken)
    {
        // First, so a version that does not exist is answered before the orders are read.
        int unscheduledLotCount = (await unscheduledLots.ListAsync(versionId, cancellationToken)).Count;
        int conflictedPlacementCount = (await placementConflicts.FindConflictsAsync(versionId, cancellationToken)).Count;
        // One now for today and for every late flag, so an order past midnight is overdue and late together.
        DateTimeOffset now = clock.GetUtcNow();
        DateOnly today = PlantTime.DateOf(now, masterData.PlantTimeZoneId);
        DateOnly dueSoonUntil = today.AddDays(DueSoonDays);
        IReadOnlyList<OrderSummaryResponse> unfinishedOrders = await orderViews.ListUnfinishedDueByAsync(versionId, dueSoonUntil, now, cancellationToken);
        IReadOnlyList<ResourceViewResponse> activeMachines = await machineViews.ListAsync(resourceTypeId: null, isActive: true, cancellationToken);
        return new DashboardResponse
        {
            DueSoonUntil = dueSoonUntil,
            OverdueOrders = [.. unfinishedOrders.Where(order => order.DueDate < today)],
            DueSoonOrders = [.. unfinishedOrders.Where(order => order.DueDate >= today)],
            UnscheduledLotCount = unscheduledLotCount,
            ConflictedPlacementCount = conflictedPlacementCount,
            DownMachines = [.. activeMachines.Where(machine => machine.Status == ResourceStatus.Down)],
        };
    }
}

using TireScheduler.Api.Queries.Machines;
using TireScheduler.Api.Queries.Orders;

namespace TireScheduler.Api.Queries.Dashboard;

// The planner dashboard of a version (F-RPT-1, DEC-65): the orders that need attention, the size of
// the queue and of the conflict list, and the machines down. Named required properties (DEC-34).
public sealed record DashboardResponse
{
    // The last due date of the due-soon orders, a fixed number of plant-local days after today (A49).
    public required DateOnly DueSoonUntil { get; init; }

    public required IReadOnlyList<OrderSummaryResponse> OverdueOrders { get; init; }

    public required IReadOnlyList<OrderSummaryResponse> DueSoonOrders { get; init; }

    public required int UnscheduledLotCount { get; init; }

    public required int ConflictedPlacementCount { get; init; }

    public required IReadOnlyList<ResourceViewResponse> DownMachines { get; init; }
}

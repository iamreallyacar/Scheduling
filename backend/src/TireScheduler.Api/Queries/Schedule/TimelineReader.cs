using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Progress;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries.Machines;
using TireScheduler.Api.Queries.Orders;
using TireScheduler.Engine;

namespace TireScheduler.Api.Queries.Schedule;

// The timeline (F-SCH-2, AC-185): the machines by type, as the machine reads list them, with what
// overlaps the range. Conflicts are not part of it; the page marks them from the conflict list (DEC-46).
internal sealed class TimelineReader(
    IPlacementReader placements,
    IUnavailabilityReader unavailabilities,
    IWorkingPeriodReader workingPeriods,
    IOrderReader orders,
    ILotProgressReader progress,
    IMasterDataReader masterData,
    MachineViewReader machines,
    TimeProvider clock)
{
    public async Task<TimelineResponse> GetAsync(string versionId, TimelineRange range, CancellationToken cancellationToken)
    {
        string plantTimeZoneId = masterData.PlantTimeZoneId;
        DateTimeOffset rangeStart = PlantTime.StartOfDay(range.From, plantTimeZoneId);
        DateTimeOffset rangeEnd = PlantTime.EndOfDay(range.To, plantTimeZoneId);
        // First, so a version that does not exist is answered before anything else is read.
        IReadOnlyList<PlacementValues> placementsInRange = await placements.PlacementsOverlappingAsync(versionId, rangeStart, rangeEnd, cancellationToken);
        ILookup<int, TimelinePlacementResponse> placementsByResource = (await DescribeAsync(versionId, placementsInRange, cancellationToken))
            .ToLookup(placement => placement.ResourceId, placement => placement.Response);
        ILookup<int, UnavailabilityResponse> unavailabilitiesByResource = (await unavailabilities.OverlappingAsync(rangeStart, rangeEnd, cancellationToken))
            .ToLookup(unavailability => unavailability.ResourceId);
        IReadOnlyDictionary<int, IReadOnlyList<WorkingPeriod>> workingPeriodsByCalendar = await workingPeriods.WorkingPeriodsByCalendarAsync(rangeStart, rangeEnd, cancellationToken);

        // Listed by type name, then name (AC-30); an inactive machine with nothing to show is left out (A43).
        IEnumerable<ResourceViewResponse> shownMachines = (await machines.ListAsync(resourceTypeId: null, isActive: null, cancellationToken))
            .Where(machine => machine.IsActive || placementsByResource.Contains(machine.Id));
        return new TimelineResponse
        {
            Start = rangeStart,
            End = rangeEnd,
            ResourceTypes =
            [
                .. shownMachines
                    .GroupBy(machine => machine.ResourceType.Id)
                    .Select(machinesOfType => new TimelineResourceTypeResponse
                    {
                        Id = machinesOfType.Key,
                        Name = machinesOfType.First().ResourceType.Name,
                        Resources =
                        [
                            .. machinesOfType.Select(machine => new TimelineResourceResponse
                            {
                                Id = machine.Id,
                                Name = machine.Name,
                                IsActive = machine.IsActive,
                                Status = machine.Status,
                                WorkingPeriods = [.. workingPeriodsByCalendar[machine.Calendar.Id].Select(period => new TimePeriodResponse { Start = period.Start, End = period.End })],
                                Unavailabilities = [.. unavailabilitiesByResource[machine.Id]],
                                Placements = [.. placementsByResource[machine.Id]],
                            }),
                        ],
                    }),
            ],
        };
    }

    // In the order of placementsInRange. The late flag needs every lot of the order in the
    // version, also those outside the range.
    private async Task<IReadOnlyList<(int ResourceId, TimelinePlacementResponse Response)>> DescribeAsync(
        string versionId,
        IReadOnlyList<PlacementValues> placementsInRange,
        CancellationToken cancellationToken)
    {
        if (placementsInRange.Count == 0)
        {
            return [];
        }

        // A live placement keeps its order from being deleted (A13), so every placed lot is found.
        IReadOnlyDictionary<int, LotContext> lotsById = await orders.FindLotContextsAsync([.. placementsInRange.Select(placement => placement.LotId)], cancellationToken);
        List<OrderSummary> ordersInRange = [.. placementsInRange.Select(placement => lotsById[placement.LotId].Order).DistinctBy(order => order.Id)];
        int[] lotIdsOfTheirOrders = [.. ordersInRange.SelectMany(order => order.LotIds)];
        IReadOnlyDictionary<int, PlacementValues> placementsOfTheirLots = await placements.PlacementsOfLotsAsync(versionId, lotIdsOfTheirOrders, cancellationToken);
        IReadOnlyDictionary<int, LotActuals> actualsOfTheirLots = await progress.ActualsOfLotsAsync(lotIdsOfTheirOrders, cancellationToken);
        DateTimeOffset now = clock.GetUtcNow();
        Dictionary<int, bool> isLateByOrder = ordersInRange.ToDictionary(
            order => order.Id,
            order => OrderLateRules.IsLate(order, placementsOfTheirLots, actualsOfTheirLots, masterData.PlantTimeZoneId, now));

        return
        [
            .. placementsInRange.Select(placement =>
            {
                LotContext lotContext = lotsById[placement.LotId];
                LotStatus status = LotStatusRules.Derive(lotContext.Order.CancelledAt, actualsOfTheirLots.GetValueOrDefault(lotContext.Lot.Id), isPlacedInVersion: true);
                return (placement.ResourceId, new TimelinePlacementResponse
                {
                    PlacementId = placement.Id,
                    PlannedStart = placement.PlannedStart,
                    PlannedEnd = placement.PlannedEnd,
                    LotId = lotContext.Lot.Id,
                    LotNo = lotContext.Lot.LotNo,
                    Quantity = lotContext.Lot.Quantity,
                    DurationMinutes = lotContext.Lot.DurationMinutes,
                    Status = status,
                    Actions = LotActionRules.Derive(status, orderIsCancelled: lotContext.Order.CancelledAt is not null, isPlacedLive: true),
                    OrderId = lotContext.Order.Id,
                    OrderNo = lotContext.Order.OrderNo,
                    Product = lotContext.Order.Product,
                    OrderIsLate = isLateByOrder[lotContext.Order.Id],
                    SequenceNo = lotContext.Operation.SequenceNo,
                    ResourceType = lotContext.Operation.ResourceType,
                });
            }),
        ];
    }
}

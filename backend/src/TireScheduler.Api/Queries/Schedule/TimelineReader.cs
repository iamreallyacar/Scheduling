using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
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
    IOrderReader orders,
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
        IReadOnlyDictionary<int, IReadOnlyList<TimePeriodResponse>> workingPeriodsByCalendar = await WorkingPeriodsAsync(rangeStart, rangeEnd, cancellationToken);

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
                                WorkingPeriods = workingPeriodsByCalendar[machine.Calendar.Id],
                                Unavailabilities = [.. unavailabilitiesByResource[machine.Id]],
                                Placements = [.. placementsByResource[machine.Id]],
                            }),
                        ],
                    }),
            ],
        };
    }

    // In the order of placementsInRange. The late flag needs every placed lot of the order in the
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
        Dictionary<int, (OrderDetails Order, OperationDetails Operation, LotDetails Lot)> lotsById = (await orders.ListAllDetailsAsync(cancellationToken))
            .SelectMany(order => order.Operations.SelectMany(operation => operation.Lots.Select(lot => (Order: order, Operation: operation, Lot: lot))))
            .ToDictionary(entry => entry.Lot.Id);
        List<OrderSummary> ordersInRange = [.. placementsInRange.Select(placement => lotsById[placement.LotId].Order.Order).DistinctBy(order => order.Id)];
        IReadOnlyDictionary<int, PlacementValues> placementsOfTheirLots = await placements.PlacementsOfLotsAsync(
            versionId,
            [.. ordersInRange.SelectMany(order => order.LotIds)],
            cancellationToken);
        DateTimeOffset now = clock.GetUtcNow();
        Dictionary<int, bool> isLateByOrder = ordersInRange.ToDictionary(
            order => order.Id,
            order => OrderLateRules.IsLate(order, placementsOfTheirLots, masterData.PlantTimeZoneId, now));

        return
        [
            .. placementsInRange.Select(placement =>
            {
                (OrderDetails order, OperationDetails operation, LotDetails lot) = lotsById[placement.LotId];
                return (placement.ResourceId, new TimelinePlacementResponse
                {
                    PlacementId = placement.Id,
                    PlannedStart = placement.PlannedStart,
                    PlannedEnd = placement.PlannedEnd,
                    LotId = lot.Id,
                    LotNo = lot.LotNo,
                    Quantity = lot.Quantity,
                    DurationMinutes = lot.DurationMinutes,
                    Status = LotStatusRules.Derive(order.Order.CancelledAt, isPlacedInVersion: true),
                    OrderId = order.Order.Id,
                    OrderNo = order.Order.OrderNo,
                    Product = order.Order.Product,
                    OrderIsLate = isLateByOrder[order.Order.Id],
                    SequenceNo = operation.SequenceNo,
                    ResourceType = operation.ResourceType,
                });
            }),
        ];
    }

    // Only the engine reads the plant-local shift times (ARCHITECTURE.md section "Time rules").
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<TimePeriodResponse>>> WorkingPeriodsAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken)
    {
        PlantWorkingTime workingTime = await masterData.GetWorkingTimeAsync(cancellationToken);
        HashSet<DateOnly> holidays = [.. workingTime.Holidays];
        return workingTime.Calendars.ToDictionary(
            calendar => calendar.Id,
            IReadOnlyList<TimePeriodResponse> (calendar) =>
            [
                .. WorkingPeriods.Between(
                        new SnapshotCalendar(calendar.Id, [.. calendar.Shifts.Select(shift => new SnapshotShift(shift.Weekday, shift.StartMinuteOfDay, shift.EndMinuteOfDay))]),
                        holidays,
                        masterData.PlantTimeZoneId,
                        rangeStart,
                        rangeEnd)
                    .Select(period => new TimePeriodResponse { Start = period.Start, End = period.End }),
            ]);
    }
}

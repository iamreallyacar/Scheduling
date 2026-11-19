using TireScheduler.Engine;

namespace TireScheduler.PlantGenerator;

// A generated plant as the engine's input, with nothing placed (AC-193), so engine tests and the
// generator can run the engine on it without a database (DEC-32). Ids are numbered from 1 in the
// order of the generated lists, because they exist only once the loader has written the rows.
public static class PlantSnapshot
{
    private const int PlantCalendarId = 1;

    public static ScheduleSnapshot From(GeneratedPlant plant, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(plant);
        Dictionary<string, int> machineTypeIds = plant.MachineTypes
            .Select((type, index) => (type.Name, Id: index + 1))
            .ToDictionary(type => type.Name, type => type.Id);
        SnapshotResource[] resources = [.. plant.Machines.Select((machine, index) =>
            new SnapshotResource(index + 1, machine.Name, machineTypeIds[machine.MachineTypeName], PlantCalendarId, IsActive: true))];
        Dictionary<string, int> resourceIds = resources.ToDictionary(resource => resource.Name, resource => resource.Id);
        Dictionary<string, GeneratedProduct> productByCode = plant.Products.ToDictionary(product => product.Code);

        List<SnapshotLot> lots = [];
        List<StageDependency> dependencies = [];
        foreach (GeneratedOrder order in plant.Orders)
        {
            GeneratedProduct product = productByCode[order.ProductCode];
            Dictionary<int, int> operationIdBySequenceNo = [];
            foreach (GeneratedStage stage in product.Stages)
            {
                // One lot per operation (A6), so the lot takes its operation's number; nothing relies on
                // the two being equal.
                int operationId = lots.Count + 1;
                operationIdBySequenceNo[stage.SequenceNo] = operationId;
                long durationMinutes = LotDuration.Minutes(stage.SetupMinutes, stage.CycleMinutes, stage.UnitsPerCycle, order.Quantity);
                lots.Add(new SnapshotLot(
                    operationId,
                    operationId,
                    stage.SequenceNo,
                    checked((int)durationMinutes),
                    machineTypeIds[stage.MachineTypeName],
                    LotProgressState.NotStarted,
                    OrderIsCancelled: false));
            }

            dependencies.AddRange(product.Dependencies.Select(dependency => new StageDependency(
                operationIdBySequenceNo[dependency.PredecessorSequenceNo],
                operationIdBySequenceNo[dependency.SuccessorSequenceNo],
                dependency.MinLagMinutes)));
        }

        return new ScheduleSnapshot(
            timeZoneId,
            [new SnapshotCalendar(PlantCalendarId, [.. plant.Shifts.Select(shift => new SnapshotShift(shift.Weekday, shift.StartMinute, shift.EndMinute))])],
            plant.Holidays.Select(holiday => holiday.Date).ToHashSet(),
            resources,
            [.. plant.Unavailabilities.Select((unavailability, index) => new SnapshotUnavailability(
                index + 1,
                resourceIds[unavailability.MachineName],
                unavailability.StartUtc(timeZoneId),
                unavailability.EndUtc(timeZoneId)))],
            lots,
            dependencies,
            []);
    }
}

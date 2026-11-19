using TireScheduler.Engine;

namespace TireScheduler.PlantGenerator.Tests;

public sealed class PlantSnapshotTests
{
    private const string Berlin = "Europe/Berlin";

    [Theory]
    [MemberData(nameof(GeneratorCases.PresetsWithSeeds), MemberType = typeof(GeneratorCases))]
    public void AC_193_a_generated_plant_becomes_an_unplaced_snapshot_with_its_calendar_machines_lots_dependencies_and_downtime(string presetName, int seed, DateOnly startDate)
    {
        GeneratedPlant plant = GeneratorCases.Generate(presetName, seed, startDate);

        ScheduleSnapshot snapshot = PlantSnapshot.From(plant, Berlin);

        Assert.Equal(Berlin, snapshot.PlantTimeZoneId);
        SnapshotCalendar calendar = Assert.Single(snapshot.Calendars);
        Assert.Equal(plant.Shifts.Select(shift => new SnapshotShift(shift.Weekday, shift.StartMinute, shift.EndMinute)), calendar.Shifts);
        Assert.Equal(plant.Holidays.Select(holiday => holiday.Date).ToHashSet(), snapshot.Holidays);
        Assert.Empty(snapshot.Placements);

        Assert.Equal(plant.Machines.Select(machine => machine.Name), snapshot.Resources.Select(resource => resource.Name));
        Assert.All(snapshot.Resources, resource =>
        {
            Assert.True(resource.IsActive);
            Assert.Equal(calendar.Id, resource.CalendarId);
        });
        Dictionary<string, int> typeIdByMachineType = TypeIdOfEachMachineType(plant, snapshot);
        Assert.Equal(plant.MachineTypes.Count, typeIdByMachineType.Values.Distinct().Count());

        AssertLotsAndDependenciesFollowTheOrders(plant, snapshot, typeIdByMachineType);

        Dictionary<string, int> resourceIdByName = snapshot.Resources.ToDictionary(resource => resource.Name, resource => resource.Id);
        Assert.Equal(
            plant.Unavailabilities.Select(unavailability => (resourceIdByName[unavailability.MachineName], PlantTime.LocalToUtc(unavailability.LocalStart, Berlin), unavailability.DurationMinutes)),
            snapshot.Unavailabilities.Select(unavailability => (unavailability.ResourceId, unavailability.Start, (int?)(unavailability.End - unavailability.Start)?.TotalMinutes)));
        Assert.Equal(snapshot.Unavailabilities.Count, snapshot.Unavailabilities.Select(unavailability => unavailability.Id).Distinct().Count());
    }

    // Every machine of a type gets the same resource type, so the type of a machine type is read
    // from its machines.
    private static Dictionary<string, int> TypeIdOfEachMachineType(GeneratedPlant plant, ScheduleSnapshot snapshot)
    {
        Dictionary<string, int> typeIdByMachineType = [];
        foreach ((GeneratedMachine machine, SnapshotResource resource) in plant.Machines.Zip(snapshot.Resources))
        {
            Assert.Equal(typeIdByMachineType.GetValueOrDefault(machine.MachineTypeName, resource.ResourceTypeId), resource.ResourceTypeId);
            typeIdByMachineType[machine.MachineTypeName] = resource.ResourceTypeId;
        }

        return typeIdByMachineType;
    }

    // The lots follow the orders and their stages in generated order (PlantSnapshot).
    private static void AssertLotsAndDependenciesFollowTheOrders(GeneratedPlant plant, ScheduleSnapshot snapshot, Dictionary<string, int> typeIdByMachineType)
    {
        Dictionary<string, GeneratedProduct> productByCode = plant.Products.ToDictionary(product => product.Code);
        List<StageDependency> expectedDependencies = [];
        int lotIndex = 0;
        foreach (GeneratedOrder order in plant.Orders)
        {
            GeneratedProduct product = productByCode[order.ProductCode];
            Dictionary<int, SnapshotLot> lotBySequenceNo = [];
            foreach (GeneratedStage stage in product.Stages)
            {
                SnapshotLot lot = snapshot.Lots[lotIndex++];
                Assert.Equal(LotDuration.Minutes(stage.SetupMinutes, stage.CycleMinutes, stage.UnitsPerCycle, order.Quantity), lot.DurationMinutes);
                Assert.Equal(typeIdByMachineType[stage.MachineTypeName], lot.ResourceTypeId);
                Assert.Equal(LotProgressState.NotStarted, lot.Progress);
                Assert.False(lot.OrderIsCancelled);
                lotBySequenceNo[stage.SequenceNo] = lot;
            }

            expectedDependencies.AddRange(product.Dependencies.Select(dependency => new StageDependency(
                lotBySequenceNo[dependency.PredecessorSequenceNo].OperationId,
                lotBySequenceNo[dependency.SuccessorSequenceNo].OperationId,
                dependency.MinLagMinutes)));
        }

        Assert.Equal(lotIndex, snapshot.Lots.Count);
        Assert.Equal(snapshot.Lots.Count, snapshot.Lots.Select(lot => lot.Id).Distinct().Count());
        Assert.Equal(snapshot.Lots.Count, snapshot.Lots.Select(lot => lot.OperationId).Distinct().Count());
        Assert.Equal(expectedDependencies, snapshot.Dependencies);
    }
}

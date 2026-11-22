using TireScheduler.Engine;

namespace TireScheduler.PlantGenerator;

public sealed record MachineTypeLoad(string MachineTypeName, long DemandMinutes, long CapacityMinutes);

// How much of each machine type's working time the orders need (A34).
public static class PlantLoad
{
    public static IReadOnlyList<MachineTypeLoad> Measure(GeneratedPlant plant, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(plant);
        Dictionary<string, GeneratedProduct> productByCode = plant.Products.ToDictionary(product => product.Code);
        Dictionary<string, long> demandByType = plant.MachineTypes.ToDictionary(type => type.Name, _ => 0L);
        foreach (GeneratedOrder order in plant.Orders)
        {
            foreach (GeneratedStage stage in productByCode[order.ProductCode].Stages)
            {
                demandByType[stage.MachineTypeName] += LotDuration.Minutes(stage.SetupMinutes, stage.CycleMinutes, stage.UnitsPerCycle, order.Quantity);
            }
        }

        long workingMinutesPerMachine = WorkingMinutesPerMachine(plant, timeZoneId);
        return plant.MachineTypes
            .Select(type => new MachineTypeLoad(
                type.Name,
                demandByType[type.Name],
                plant.Machines.Count(machine => machine.MachineTypeName == type.Name) * workingMinutesPerMachine))
            .ToArray();
    }

    // The working periods the engine places lots in (R6), so a DST day counts its elapsed minutes
    // and the load follows any change to the working-time rule.
    private static long WorkingMinutesPerMachine(GeneratedPlant plant, string timeZoneId)
    {
        ScheduleSnapshot snapshot = PlantSnapshot.From(plant, timeZoneId);
        IReadOnlyList<WorkingPeriod> periods = WorkingPeriods.Between(
            snapshot.Calendars.Single(),
            snapshot.Holidays,
            timeZoneId,
            PlantTime.StartOfDay(plant.StartDate, timeZoneId),
            PlantTime.StartOfDay(plant.EndDate, timeZoneId));
        return periods.Sum(period => (long)(period.End - period.Start).TotalMinutes);
    }
}

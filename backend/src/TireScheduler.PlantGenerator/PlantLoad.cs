using TireScheduler.Engine;

namespace TireScheduler.PlantGenerator;

public sealed record MachineTypeLoad(string MachineTypeName, long DemandMinutes, long CapacityMinutes);

// How much of each machine type's working time the orders need (A34).
public static class PlantLoad
{
    public static IReadOnlyList<MachineTypeLoad> Measure(GeneratedPlant plant)
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

        long workingMinutesPerMachine = WorkingMinutesPerMachine(plant);
        return plant.MachineTypes
            .Select(type => new MachineTypeLoad(
                type.Name,
                demandByType[type.Name],
                plant.Machines.Count(machine => machine.MachineTypeName == type.Name) * workingMinutesPerMachine))
            .ToArray();
    }

    private static long WorkingMinutesPerMachine(GeneratedPlant plant)
    {
        HashSet<DateOnly> holidays = plant.Holidays.Select(holiday => holiday.Date).ToHashSet();
        long minutes = 0;
        for (DateOnly day = plant.StartDate; day < plant.EndDate; day = day.AddDays(1))
        {
            if (!holidays.Contains(day))
            {
                minutes += plant.Shifts.Where(shift => shift.Weekday == day.DayOfWeek).Sum(shift => shift.EndMinute - shift.StartMinute);
            }
        }

        return minutes;
    }
}

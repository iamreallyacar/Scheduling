using System.Globalization;

namespace TireScheduler.PlantGenerator;

// Builds a consistent plant from parameters. The same parameters, seed and start date always
// give the same plant (AC-151), so tests and bug reports can name a seed.
public static class SyntheticPlant
{
    public static GeneratedPlant Generate(PlantParameters parameters, int seed, DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        PlantParameterChecks.ThrowIfInvalid(parameters, startDate);

        // The steps draw from one sequence in a fixed order.
        SeededRandom random = new(seed);

        GeneratedMachine[] machines = parameters.MachineTypes
            .SelectMany(type => Enumerable.Range(1, type.MachineCount)
                .Select(number => new GeneratedMachine(string.Create(CultureInfo.InvariantCulture, $"{type.MachineNamePrefix} {number}"), type.Name)))
            .ToArray();
        GeneratedProduct[] products = ProductGeneration.Products(parameters, random);
        GeneratedHoliday[] holidays = CalendarGeneration.Holidays(parameters, startDate, random);
        GeneratedOrder[] orders = OrderGeneration.Orders(parameters, startDate, products, random);
        GeneratedUnavailability[] unavailabilities = DowntimeGeneration.Unavailabilities(parameters, startDate, machines, random);

        return new GeneratedPlant(
            seed,
            startDate,
            parameters.HorizonWeeks,
            [.. parameters.MachineTypes.Select(type => new GeneratedMachineType(type.Name))],
            machines,
            CalendarGeneration.Shifts(parameters),
            holidays,
            products,
            orders,
            unavailabilities);
    }
}

using System.Globalization;

namespace TireScheduler.PlantGenerator;

// Products with tire-like names and sizes, each with a linear routing drawn from the stage
// parameters.
internal static class ProductGeneration
{
    private static readonly string[] ProductLines = ["Touring", "Sport", "Winter", "All-Season", "Van", "Eco"];
    private static readonly string[] SpeedRatings = ["T", "H", "V", "W"];

    public static GeneratedProduct[] Products(PlantParameters parameters, SeededRandom random) =>
        Enumerable.Range(1, parameters.ProductCount).Select(number => Product(parameters, number, random)).ToArray();

    private static GeneratedProduct Product(PlantParameters parameters, int number, SeededRandom random)
    {
        int width = 175 + (10 * random.Next(12));
        int aspectRatio = 40 + (5 * random.Next(7));
        int rim = random.Next(15, 21);
        string name = string.Create(CultureInfo.InvariantCulture, $"{random.Pick(ProductLines)} {width}/{aspectRatio} R{rim}");
        string spec = string.Create(CultureInfo.InvariantCulture, $"Load index {random.Next(75, 106)}, speed rating {random.Pick(SpeedRatings)}");

        GeneratedStage[] stages = parameters.RoutingStages
            .Select((stage, position) => new GeneratedStage(
                position + 1,
                stage.MachineTypeName,
                random.Draw(stage.SetupMinutes),
                random.Draw(stage.CycleMinutes),
                random.Draw(stage.UnitsPerCycle)))
            .ToArray();
        GeneratedDependency[] dependencies = parameters.StageLagMinutes
            .Select((lag, position) => new GeneratedDependency(position + 1, position + 2, random.Draw(lag)))
            .ToArray();

        return new GeneratedProduct(string.Create(CultureInfo.InvariantCulture, $"TR-{number:0000}"), name, spec, stages, dependencies);
    }
}

namespace TireScheduler.PlantGenerator.Tests;

// Every preset with a few seeds and start dates. 2027-03-22 is the week of the spring-forward
// change in Europe/Berlin, 2026-10-26 the week after the fall-back change.
public static class GeneratorCases
{
    public static readonly DateOnly StartMonday = new(2026, 11, 16);

    public static TheoryData<string, int, DateOnly> PresetsWithSeeds()
    {
        TheoryData<string, int, DateOnly> cases = [];
        foreach (string presetName in PlantPresets.ByName.Keys)
        {
            cases.Add(presetName, 1, StartMonday);
            cases.Add(presetName, 42, new DateOnly(2027, 3, 22));
            cases.Add(presetName, 2026, new DateOnly(2026, 10, 26));
            cases.Add(presetName, -7, new DateOnly(2026, 11, 18));
        }

        return cases;
    }

    public static GeneratedPlant Generate(string presetName, int seed, DateOnly startDate) =>
        SyntheticPlant.Generate(PlantPresets.ByName[presetName], seed, startDate);
}

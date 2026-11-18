using System.Text.Json;

namespace TireScheduler.PlantGenerator.Tests;

public sealed class GeneratorDeterminismTests
{
    [Theory]
    [MemberData(nameof(GeneratorCases.PresetsWithSeeds), MemberType = typeof(GeneratorCases))]
    public void AC_151_the_same_seed_gives_the_same_plant_and_another_seed_a_different_one(string presetName, int seed, DateOnly startDate)
    {
        GeneratedPlant first = GeneratorCases.Generate(presetName, seed, startDate);
        GeneratedPlant second = GeneratorCases.Generate(presetName, seed, startDate);
        GeneratedPlant otherSeed = GeneratorCases.Generate(presetName, seed + 1, startDate);

        Assert.Equal(Serialized(first), Serialized(second));
        Assert.NotEqual(Serialized(first), Serialized(otherSeed with { Seed = seed }));
    }

    // The records hold lists, which compare by reference, so the plants are compared as JSON.
    private static string Serialized(GeneratedPlant plant) => JsonSerializer.Serialize(plant);
}

using System.Reflection;

namespace TireScheduler.Architecture.Tests;

// The generator core is pure (DEC-32): it may use the engine and nothing of the API, the
// database or the web stack, and the engine and the API never use it.
public sealed class PlantGeneratorIndependenceTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
        ["TireScheduler.Api", "TireScheduler.PlantLoader", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "System.Net.Http"];

    [Fact]
    public void Generator_core_references_only_the_engine_of_the_project_assemblies()
    {
        string[] forbiddenReferences = ReferencedNames("TireScheduler.PlantGenerator")
            .Where(name => ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Theory]
    [InlineData("TireScheduler.Engine")]
    [InlineData("TireScheduler.Api")]
    public void Neither_the_engine_nor_the_api_uses_the_generator(string assemblyName)
    {
        Assert.DoesNotContain(ReferencedNames(assemblyName), name => name.StartsWith("TireScheduler.Plant", StringComparison.Ordinal));
    }

    private static IEnumerable<string> ReferencedNames(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);
}

using System.Reflection;

namespace TireScheduler.Architecture.Tests;

// The engine is a pure library (ARCHITECTURE.md section 3): no API, module, database or web references.
public sealed class EngineIndependenceTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
        ["TireScheduler.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql"];

    [Fact]
    public void Engine_references_no_api_database_or_web_assembly()
    {
        Assembly engineAssembly = Assembly.Load("TireScheduler.Engine");

        string[] forbiddenReferences = engineAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }
}

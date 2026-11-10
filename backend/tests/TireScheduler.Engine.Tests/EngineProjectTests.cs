using System.Reflection;

namespace TireScheduler.Engine.Tests;

// Placeholder until task M3-1 adds the engine and its worked-example tests. It only proves
// the test project builds, runs and loads the engine assembly.
public sealed class EngineProjectTests
{
    [Fact]
    public void Engine_assembly_loads()
    {
        Assembly engineAssembly = Assembly.Load("TireScheduler.Engine");

        Assert.Equal("TireScheduler.Engine", engineAssembly.GetName().Name);
    }
}

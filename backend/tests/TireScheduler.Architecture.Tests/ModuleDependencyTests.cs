using System.Reflection;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace TireScheduler.Architecture.Tests;

// Enforces the module dependency table in ARCHITECTURE.md section 2: dependencies point
// downward only. Update the table and this data together.
public sealed class ModuleDependencyTests
{
    private const string ModulesNamespace = "TireScheduler.Api.Modules";

    private static readonly string[] AllModules = ["Auth", "MasterData", "Orders", "Scheduling", "Progress"];

    private static readonly Dictionary<string, string[]> AllowedDependencies = new()
    {
        ["Auth"] = [],
        ["MasterData"] = [],
        ["Orders"] = ["MasterData"],
        ["Scheduling"] = ["MasterData", "Orders"],
        ["Progress"] = ["Orders", "Scheduling"],
    };

    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    public static TheoryData<string> ModuleNames => new(AllModules);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_depends_only_on_modules_listed_below_it(string moduleName)
    {
        string[] forbiddenNamespaces = AllModules
            .Where(other => other != moduleName && !AllowedDependencies[moduleName].Contains(other))
            .Select(other => $"{ModulesNamespace}.{other}")
            .Append("TireScheduler.Api.Queries")
            .ToArray();

        ArchTestResult result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace($"{ModulesNamespace}.{moduleName}")
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Every_module_has_a_dependency_entry()
    {
        Assert.Equal(AllModules.Order(), AllowedDependencies.Keys.Order());
    }

    private static string FailureMessage(ArchTestResult result) =>
        "Types breaking the module dependency table: " + string.Join(", ", result.FailingTypeNames ?? []);
}

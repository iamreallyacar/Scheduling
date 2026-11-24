using System.Reflection;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace TireScheduler.Architecture.Tests;

// Enforces the module dependency table in ARCHITECTURE.md section "Structure": dependencies point
// downward only. Update the table and this data together.
public sealed class ModuleDependencyTests
{
    private const string ModulesNamespace = "TireScheduler.Api.Modules";

    private static readonly Dictionary<string, string[]> AllowedDependencies = new()
    {
        ["Auth"] = [],
        ["MasterData"] = [],
        ["Orders"] = ["MasterData"],
        ["Scheduling"] = ["MasterData", "Orders"],
        ["Progress"] = ["Orders", "Scheduling"],
    };

    // The modules whose row in the table lists the engine (DEC-28). Queries, which may read every
    // module, uses it too (DEC-30); it is not a module, so these tests do not cover it.
    private static readonly string[] ModulesUsingTheEngine = ["Orders", "Scheduling"];

    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    // Read from the code rather than typed in, so a new module folder without a row in the table
    // fails Every_module_has_a_dependency_entry instead of escaping every rule.
    private static readonly string[] AllModules = ApiAssembly.GetTypes()
        .Select(type => type.Namespace)
        .OfType<string>()
        .Where(name => name.StartsWith($"{ModulesNamespace}.", StringComparison.Ordinal))
        .Select(name => name[(ModulesNamespace.Length + 1)..].Split('.')[0])
        .Distinct()
        .ToArray();

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

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Only_modules_listed_with_the_engine_use_it(string moduleName)
    {
        if (ModulesUsingTheEngine.Contains(moduleName))
        {
            return;
        }

        ArchTestResult result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace($"{ModulesNamespace}.{moduleName}")
            .ShouldNot().HaveDependencyOn("TireScheduler.Engine")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    // The folders every module may use (ARCHITECTURE.md section "Structure") must not know any module,
    // or a module could reach another one through them.
    [Theory]
    [InlineData("TireScheduler.Api.Errors")]
    [InlineData("TireScheduler.Api.Security")]
    [InlineData("TireScheduler.Api.Database")]
    public void Shared_folder_depends_on_no_module(string sharedNamespace)
    {
        ArchTestResult result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace(sharedNamespace)
            .ShouldNot().HaveDependencyOnAny(ModulesNamespace, "TireScheduler.Api.Queries")
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

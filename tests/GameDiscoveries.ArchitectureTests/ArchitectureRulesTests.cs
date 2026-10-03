using FluentAssertions;
using NetArchTest.Rules;

namespace GameDiscoveries.ArchitectureTests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void BuildingBlocks_should_not_reference_modules_infrastructure_or_api_assemblies()
    {
        var references = typeof(BuildingBlocks.DependencyInjection).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToHashSet(StringComparer.Ordinal);

        references.Should().NotContain(name =>
            name.StartsWith("GameDiscoveries.Modules", StringComparison.Ordinal)
            || name.Equals("GameDiscoveries.Infrastructure", StringComparison.Ordinal)
            || name.Equals("GameDiscoveries.Api", StringComparison.Ordinal));
    }

    [Fact]
    public void Modules_should_not_depend_on_api_or_infrastructure()
    {
        var result = Types.InAssembly(typeof(Modules.Catalog.CatalogModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "GameDiscoveries.Api",
                "GameDiscoveries.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_api()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("GameDiscoveries.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_modules()
    {
        var references = typeof(Infrastructure.DependencyInjection).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToHashSet(StringComparer.Ordinal);

        references.Should().NotContain(name =>
            name.StartsWith("GameDiscoveries.Modules", StringComparison.Ordinal));
    }

    [Fact]
    public void Api_references_modules_and_building_blocks()
    {
        var references = typeof(Program).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToHashSet(StringComparer.Ordinal);

        references.Should().Contain("GameDiscoveries.Modules.Catalog");
        references.Should().Contain("GameDiscoveries.BuildingBlocks");
        references.Should().Contain("GameDiscoveries.Infrastructure");
    }

    private static string FormatFailures(TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}

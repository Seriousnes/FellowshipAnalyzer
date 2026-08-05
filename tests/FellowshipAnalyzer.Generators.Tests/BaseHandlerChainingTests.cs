using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

/// <summary>
/// A module that declares its own <c>[On&lt;&gt;]</c> handlers must keep the ones its base class
/// declares. The generated <c>RegisterAttributeSubscriptions</c> is an override, so without a chained
/// call to the base every handler on a shared base module - a <c>ResourceTracker</c> or a
/// <c>HotTracker</c> - silently stops subscribing the moment a hero adds one of its own, and the
/// derived module reads zeroes with nothing to show for it.
/// </summary>
public class BaseHandlerChainingTests
{
    private const string Usings = """
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        using OneOf;
        """;

    [Fact]
    public void DerivedModule_ChainsToItsBaseSubscriptions()
    {
        var source = Usings + """

            namespace Test;
            public partial class BaseModule : Analyzer
            {
                [On<CastEvent>]
                private void OnBaseCast(CastEvent e) { }
            }

            public partial class DerivedModule : BaseModule
            {
                [On<HealEvent>]
                private void OnDerivedHeal(HealEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        result.ConcatenatedGenerated.ShouldContain("partial class DerivedModule");
        result.ConcatenatedGenerated.ShouldContain("base.RegisterAttributeSubscriptions();");
        result.ConcatenatedGenerated.ShouldContain("OnDerivedHeal");
        result.ConcatenatedGenerated.ShouldContain("OnBaseCast");
        AssertNoErrors(result);
    }

    [Fact]
    public void ModuleWithNoBaseHandlers_StillChainsToTheEmptyBase()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(CastEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        result.ConcatenatedGenerated.ShouldContain("base.RegisterAttributeSubscriptions();");
        AssertNoErrors(result);
    }

    private static void AssertNoErrors(GeneratorRunResult result)
    {
        result.DriverDiagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.CompilationDiagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
    }
}

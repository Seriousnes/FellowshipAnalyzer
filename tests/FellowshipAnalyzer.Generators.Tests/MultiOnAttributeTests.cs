using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class MultiOnAttributeTests
{
    private const string Usings = """
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        using OneOf;
        """;

    [Fact]
    public void SingleAttribute_ConcreteParam_EmitsOneSubscribe()
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

        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.CastEvent __e0");
        result.ConcatenatedGenerated.ShouldContain("H((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
    }

    [Fact]
    public void TwoAttributes_InterfaceParam_EmitsTwoSubscribes()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>, On<HealEvent>]
                private void H(IAbilityEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(2);
        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.CastEvent");
        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.HealEvent");
        result.ConcatenatedGenerated.ShouldContain("H((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
        result.ConcatenatedGenerated.ShouldContain("H((global::FellowshipAnalyzer.Core.Events.HealEvent)e)");
        AssertNoErrors(result);
    }

    [Fact]
    public void TwoAttributes_IHasSourceEventParam_EmitsTwoSubscribes()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>, On<ApplyBuffEvent>]
                private void H(IHasSourceEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(2);
        AssertNoErrors(result);
    }

    [Fact]
    public void TwoAttributes_OneIncompatibleInterface_DropsBadAttribute()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>, On<DeathEvent>]
                private void H(IHasSourceEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.CastEvent");
        result.ConcatenatedGenerated.ShouldNotContain("DeathEvent");
        AssertNoErrors(result);
    }

    [Fact]
    public void SingleAttribute_InterfaceParam_EmitsOneSubscribe()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(IHasSourceEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
    }

    [Fact]
    public void OneOfParam_ThreeExactSlots_EmitsThreeSubscribesWithFromTN()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>, On<HealEvent>, On<ApplyBuffEvent>]
                private void H(OneOf<CastEvent, HealEvent, ApplyBuffEvent> e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(3);
        result.ConcatenatedGenerated.ShouldContain(
            ".FromT0((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
        result.ConcatenatedGenerated.ShouldContain(
            ".FromT1((global::FellowshipAnalyzer.Core.Events.HealEvent)e)");
        result.ConcatenatedGenerated.ShouldContain(
            ".FromT2((global::FellowshipAnalyzer.Core.Events.ApplyBuffEvent)e)");
        AssertNoErrors(result);
    }

    [Fact]
    public void OneOfParam_UnusedSlotAllowed()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>, On<HealEvent>]
                private void H(OneOf<CastEvent, HealEvent, AbsorbedEvent> e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(2);
        result.ConcatenatedGenerated.ShouldContain(".FromT0((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
        result.ConcatenatedGenerated.ShouldContain(".FromT1((global::FellowshipAnalyzer.Core.Events.HealEvent)e)");
        result.ConcatenatedGenerated.ShouldNotContain(".FromT2");
        AssertNoErrors(result);
    }

    [Fact]
    public void OneOfParam_NoCompatibleSlot_DropsHandler()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(OneOf<HealEvent, DamageEvent> e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(0);
    }

    [Fact]
    public void OneOfParam_AmbiguousInterfaceSlots_DropsHandler()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(OneOf<IHasSourceEvent, IAbilityEvent> e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(0);
    }

    [Fact]
    public void OneOfParam_MostDerivedSlotResolves()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(OneOf<BaseCastEvent, HealEvent> e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        result.ConcatenatedGenerated.ShouldContain(".FromT0((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
        AssertNoErrors(result);
    }

    [Fact]
    public void SingleAttribute_EventBaseParam_EmitsOneSubscribe()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(Event e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        result.ConcatenatedGenerated.ShouldContain("H((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
        AssertNoErrors(result);
    }

    private static int SubscribeCallCount(GeneratorRunResult result)
    {
        var src = result.ConcatenatedGenerated;
        var count = 0;
        var index = 0;
        const string needle = "__emitter.Subscribe(this,";
        while ((index = src.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static void AssertNoErrors(GeneratorRunResult result)
    {
        var errors = result.CompilationDiagnostics
            .Concat(result.DriverDiagnostics)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        errors.ShouldBeEmpty(
            "Compilation should be error-free. Errors:\n" +
            string.Join("\n", errors.Select(d => d.ToString())));
    }
}

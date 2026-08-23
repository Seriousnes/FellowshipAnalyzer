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

    [Fact]
    public void NoParam_EmitsSubscribeCallingTheHandlerWithNoArgument()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H() { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.CastEvent __e0");
        result.ConcatenatedGenerated.ShouldContain("=> H())");
        AssertNoErrors(result);
    }

    [Fact]
    public void NoParam_KeepsTheAttributeActorFilter()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>(By = 1)]
                private void H() { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        result.ConcatenatedGenerated.ShouldContain("__owner.ByPlayer(__e0, null)");
        result.ConcatenatedGenerated.ShouldContain("=> H())");
        AssertNoErrors(result);
    }

    [Fact]
    public void NoParam_TwoAttributes_EmitsTwoSubscribes()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>, On<HealEvent>]
                private void H() { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(2);
        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.CastEvent");
        result.ConcatenatedGenerated.ShouldContain("e is global::FellowshipAnalyzer.Core.Events.HealEvent");
        AssertNoErrors(result);
    }

    [Fact]
    public void TwoParams_DropsHandler()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H(CastEvent e, int extra) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(0);
    }

    [Fact]
    public void NoParam_TaskReturning_EmitsTaskDelegateCallingTheHandlerWithNoArgument()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private System.Threading.Tasks.Task H() => System.Threading.Tasks.Task.CompletedTask;
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        result.ConcatenatedGenerated.ShouldContain("global::System.Threading.Tasks.Task>)(e => H())");
        AssertNoErrors(result);
    }

    [Fact]
    public void Param_TaskReturning_EmitsTaskDelegateCallingTheHandlerWithTheEvent()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private System.Threading.Tasks.Task H(CastEvent e) => System.Threading.Tasks.Task.CompletedTask;
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
        result.ConcatenatedGenerated.ShouldContain(
            "global::System.Threading.Tasks.Task>)(e => H((global::FellowshipAnalyzer.Core.Events.CastEvent)e))");
    }

    [Fact]
    public void Param_TaskOfTReturning_EmitsTaskDelegateCallingTheHandlerWithTheEvent()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private System.Threading.Tasks.Task<int> H(CastEvent e) => System.Threading.Tasks.Task.FromResult(0);
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
        result.ConcatenatedGenerated.ShouldContain(
            "global::System.Threading.Tasks.Task>)(e => H((global::FellowshipAnalyzer.Core.Events.CastEvent)e))");
    }

    [Fact]
    public void Param_ValueTaskReturning_EmitsTaskDelegateAdaptedWithAsTask()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private System.Threading.Tasks.ValueTask H(CastEvent e) => default;
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
        result.ConcatenatedGenerated.ShouldContain(
            "global::System.Threading.Tasks.Task>)(e => H((global::FellowshipAnalyzer.Core.Events.CastEvent)e).AsTask())");
    }

    [Fact]
    public void NoParam_ValueTaskReturning_EmitsTaskDelegateAdaptedWithAsTask()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private System.Threading.Tasks.ValueTask H() => default;
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
        result.ConcatenatedGenerated.ShouldContain("global::System.Threading.Tasks.Task>)(e => H().AsTask())");
    }

    [Fact]
    public void Param_ValueTaskOfTReturning_EmitsTaskDelegateAdaptedWithAsTask()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private System.Threading.Tasks.ValueTask<int> H(CastEvent e) => default;
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
        result.ConcatenatedGenerated.ShouldContain(
            "global::System.Threading.Tasks.Task>)(e => H((global::FellowshipAnalyzer.Core.Events.CastEvent)e).AsTask())");
    }

    [Fact]
    public void UnrelatedTypeNamedTask_EmitsSynchronousDelegate()
    {
        var source = Usings + """

            namespace Unrelated
            {
                public sealed class Task { }
            }

            namespace Test
            {
                public partial class M : Analyzer
                {
                    [On<CastEvent>]
                    private global::Unrelated.Task H(CastEvent e) => null!;
                }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(1);
        AssertNoErrors(result);
        result.ConcatenatedGenerated.ShouldContain(
            "global::System.Action<global::FellowshipAnalyzer.Core.Events.Event>)(e => H((global::FellowshipAnalyzer.Core.Events.CastEvent)e))");
    }

    [Fact]
    public void NoParamAlongsideParam_EmitsBothCallForms()
    {
        var source = Usings + """

            namespace Test;
            public partial class M : Analyzer
            {
                [On<CastEvent>]
                private void H0() { }

                [On<CastEvent>]
                private void H1(CastEvent e) { }
            }
            """;
        var result = GeneratorTestHarness.Run(source);

        SubscribeCallCount(result).ShouldBe(2);
        result.ConcatenatedGenerated.ShouldContain("=> H0())");
        result.ConcatenatedGenerated.ShouldContain("H1((global::FellowshipAnalyzer.Core.Events.CastEvent)e)");
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

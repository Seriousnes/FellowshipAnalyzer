using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class ParserGeneratorTests
{
    private const string Usings = """
        using System;
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        """;

    private const string PullFqn = "global::FellowshipAnalyzer.Core.Analysis.Pull";
    private const string PullKindFqn = "global::FellowshipAnalyzer.Core.Analysis.PullKind";

    [Fact]
    public void AddAnalyzer_EmitsForPullGate_AndConstructsAnalyzer()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single)]"));
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain($"protected override global::System.Type[] GetAnalyzerTypes({PullFqn} pull)");
        gen.ShouldContain($"if ((pull.Targets & ({PullKindFqn}.Single)) != 0) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        gen.ShouldContain("if (type == typeof(global::Test.ComboAnalyzer))");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_BossOnly_EmitsBossClause()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Multi, Boss = PullBoss.Boss)]"));

        result.ConcatenatedGenerated.ShouldContain(
            $"if ((pull.Targets & ({PullKindFqn}.Multi)) != 0 && pull.IsBoss) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_NonBoss_EmitsNegatedBossClause()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single, Boss = PullBoss.NonBoss)]"));

        result.ConcatenatedGenerated.ShouldContain("!= 0 && !pull.IsBoss) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_MultipleTargets_EmitsCombinedMask()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single | PullKind.Multi)]"));

        result.ConcatenatedGenerated.ShouldContain(
            $"(pull.Targets & ({PullKindFqn}.Single | {PullKindFqn}.Multi)) != 0");
        AssertNoErrors(result);
    }

    [Fact]
    public void AnalyzerSurface_EmitsTypedIndex_AndThreeReadPaths()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single)]"));
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain("private readonly global::FellowshipAnalyzer.Core.Analysis.PullAnalyzerList<global::Test.ComboAnalyzer> _comboAnalyzers = new();");
        gen.ShouldContain("public global::System.Collections.Generic.IReadOnlyList<global::FellowshipAnalyzer.Core.Analysis.PullAnalyzer<global::Test.ComboAnalyzer>> ComboAnalyzers => _comboAnalyzers;");

        gen.ShouldContain($"protected override void IndexPullAnalyzer({PullFqn} pull, global::FellowshipAnalyzer.Core.Analysis.Analyzer analyzer)");
        gen.ShouldContain("case global::Test.ComboAnalyzer");
        gen.ShouldContain("_comboAnalyzers.Add(new global::FellowshipAnalyzer.Core.Analysis.PullAnalyzer<global::Test.ComboAnalyzer>(pull,");

        gen.ShouldContain($"public ComboPullView For({PullFqn} pull) => new(pull);");
        gen.ShouldContain($"public readonly struct ComboPullView({PullFqn} pull)");
        gen.ShouldContain($"extension({PullFqn} pull)");
        gen.ShouldContain("public global::Test.ComboAnalyzer? ComboAnalyzer => (global::Test.ComboAnalyzer?)pull.GetAnalyzer(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void TwoAnalyzers_SharedAbstractBase_ShareOneSurfaceStream()
    {
        var result = ParserGeneratorTestHarness.Run(TwoAnalyzersSharedBase());
        var gen = result.ConcatenatedGenerated;

        OccurrenceCount(gen, "_comboAnalyzers = new();").ShouldBe(1);
        OccurrenceCount(gen, "case global::Test.ComboAnalyzer").ShouldBe(1);
        gen.ShouldContain("__analyzers.Add(typeof(global::Test.StAnalyzer));");
        gen.ShouldContain("__analyzers.Add(typeof(global::Test.AoeAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void SideEffectOnlyAnalyzer_IsGated_AndIsItsOwnSurface()
    {
        var result = ParserGeneratorTestHarness.Run(SideEffectOnlyAnalyzer());
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain("__analyzers.Add(typeof(global::Test.SideAnalyzer));");
        gen.ShouldContain("global::FellowshipAnalyzer.Core.Analysis.PullAnalyzerList<global::Test.SideAnalyzer>");
        gen.ShouldContain("public global::Test.SideAnalyzer? SideAnalyzer => (global::Test.SideAnalyzer?)pull.GetAnalyzer(typeof(global::Test.SideAnalyzer));");
        AssertNoErrors(result);
    }

    private static string OneAnalyzer(string forPull) => Usings + $$"""

        namespace Test;

        [AddState<ComboState>]
        [AddAnalyzer<ComboAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        public sealed partial class ComboState : EventSubscriber
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
        }

        {{forPull}}
        public sealed partial class ComboAnalyzer : Analyzer
        {
            public int Count { get; private set; }

            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) => Count++;
        }
        """;

    private static string TwoAnalyzersSharedBase() => Usings + """

        namespace Test;

        [AddAnalyzer<StAnalyzer>]
        [AddAnalyzer<AoeAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        public abstract partial class ComboAnalyzer : Analyzer
        {
            public int Count { get; private set; }

            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) => Count++;
        }

        [ForPull(PullKind.Single)]
        public sealed partial class StAnalyzer : ComboAnalyzer { }

        [ForPull(PullKind.Multi)]
        public sealed partial class AoeAnalyzer : ComboAnalyzer { }
        """;

    private static string SideEffectOnlyAnalyzer() => Usings + """

        namespace Test;

        [AddAnalyzer<SideAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        [ForPull(PullKind.Single)]
        public sealed partial class SideAnalyzer : Analyzer
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
        }
        """;

    [Fact]
    public void Uses_GeneratesPrimaryCtorAndPascalCaseAccessor()
    {
        var result = ParserGeneratorTestHarness.Run(UsesConsumer());
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain("partial class ConsumerAnalyzer(global::System.Lazy<global::Test.DepModule> depModule)");
        gen.ShouldContain("private global::Test.DepModule DepModule => field ??= depModule.Value;");
        gen.ShouldContain(
            "new global::Test.ConsumerAnalyzer(new global::System.Lazy<global::Test.DepModule>(() => (global::Test.DepModule)ResolveAnalysisModule(typeof(global::Test.DepModule))))");
        AssertNoErrors(result);
    }

    [Fact]
    public void Uses_WithHandWrittenConstructor_WarnsFa0018AndDefersToConstructor()
    {
        var result = ParserGeneratorTestHarness.Run(UsesConflict());
        var gen = result.ConcatenatedGenerated;

        result.DriverDiagnostics.ShouldContain(d => d.Id == "FA0018");
        gen.ShouldContain("private global::Test.OtherDep _other => field ??= other.Value;");
        gen.ShouldNotContain("DepModule DepModule =>");
        gen.ShouldContain("new global::Test.ConflictAnalyzer(new global::System.Lazy<global::Test.OtherDep>");
        AssertNoErrors(result);
    }

    [Fact]
    public void Uses_MultipleDependencies_OrderedConsistentlyAcrossBothGenerators()
    {
        var result = ParserGeneratorTestHarness.Run(UsesMultiple());
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain(
            "partial class MultiAnalyzer(global::System.Lazy<global::Test.DepModule> depModule, global::System.Lazy<global::Test.OtherDep> otherDep)");
        AssertNoErrors(result);
    }

    private static string UsesConsumer() => Usings + """

        namespace Test;

        [AddModule<DepModule>]
        [AddAnalyzer<ConsumerAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        public sealed partial class DepModule : EventSubscriber
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
            public int Ping() => 1;
            public static int Beep() => 2;
        }

        [ForPull(PullKind.Single)]
        [Uses<DepModule>]
        public sealed partial class ConsumerAnalyzer : Analyzer<ComboResult>
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { _ = DepModule.Ping() + DepModule.Beep(); }
            public override ComboResult OnPullEnd() => new();
        }

        public sealed record ComboResult(int X) : IResult { public ComboResult() : this(0) { } }
        """;

    private static string UsesConflict() => Usings + """

        namespace Test;

        [AddModule<DepModule>]
        [AddModule<OtherDep>]
        [AddAnalyzer<ConflictAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        public sealed partial class DepModule : EventSubscriber
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
        }

        public sealed partial class OtherDep : EventSubscriber
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
            public int Ping() => 1;
        }

        [ForPull(PullKind.Single)]
        [Uses<DepModule>]
        public sealed partial class ConflictAnalyzer(Lazy<OtherDep> other) : Analyzer<ComboResult>
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { _ = _other.Ping(); }
            public override ComboResult OnPullEnd() => new();
        }

        public sealed record ComboResult(int X) : IResult { public ComboResult() : this(0) { } }
        """;

    private static string UsesMultiple() => Usings + """

        namespace Test;

        [AddModule<DepModule>]
        [AddModule<OtherDep>]
        [AddAnalyzer<MultiAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        public sealed partial class DepModule : EventSubscriber
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
            public int Ping() => 1;
        }

        public sealed partial class OtherDep : EventSubscriber
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
            public int Pong() => 2;
        }

        [ForPull(PullKind.Single)]
        [Uses<OtherDep>]
        [Uses<DepModule>]
        public sealed partial class MultiAnalyzer : Analyzer<ComboResult>
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { _ = DepModule.Ping() + OtherDep.Pong(); }
            public override ComboResult OnPullEnd() => new();
        }

        public sealed record ComboResult(int X) : IResult { public ComboResult() : this(0) { } }
        """;

    private static int OccurrenceCount(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
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
            "Generated code should compile error-free. Errors:\n" +
            string.Join("\n", errors.Select(d => d.ToString())));
    }
}

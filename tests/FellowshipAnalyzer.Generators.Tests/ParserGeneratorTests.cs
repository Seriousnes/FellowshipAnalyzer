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
        gen.ShouldContain($"if (({PullKindFqn}.Single).HasFlag(pull.Targets)) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        gen.ShouldContain("if (type == typeof(global::Test.ComboAnalyzer))");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_BossOnly_EmitsBossClause()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Multi, Boss = PullBoss.Boss)]"));

        result.ConcatenatedGenerated.ShouldContain(
            $"if (({PullKindFqn}.Multi).HasFlag(pull.Targets) && pull.IsBoss) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_NonBoss_EmitsNegatedBossClause()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single, Boss = PullBoss.NonBoss)]"));

        result.ConcatenatedGenerated.ShouldContain(".HasFlag(pull.Targets) && !pull.IsBoss) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_MultipleTargets_EmitsCombinedMask()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single | PullKind.Multi)]"));

        result.ConcatenatedGenerated.ShouldContain(
            $"({PullKindFqn}.Single | {PullKindFqn}.Multi).HasFlag(pull.Targets)");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_WithRequiresTalent_AppendsTalentGate()
    {
        var result = ParserGeneratorTestHarness.Run(
            OneAnalyzer("[ForPull(PullKind.Single)]\n[RequiresTalent(422)]"));

        result.ConcatenatedGenerated.ShouldContain(
            $"if (({PullKindFqn}.Single).HasFlag(pull.Targets) && SelectedCombatant.HasTalent(422)) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void ForPull_WithSeveralRequiresTalent_AndsEveryTalentGate()
    {
        var result = ParserGeneratorTestHarness.Run(
            OneAnalyzer("[ForPull(PullKind.Multi, Boss = PullBoss.Boss)]\n[RequiresTalent(422)]\n[RequiresTalent(443)]"));

        result.ConcatenatedGenerated.ShouldContain(
            ".HasFlag(pull.Targets) && pull.IsBoss && SelectedCombatant.HasTalent(422) && SelectedCombatant.HasTalent(443)) __analyzers.Add(typeof(global::Test.ComboAnalyzer));");
        AssertNoErrors(result);
    }

    [Fact]
    public void AnalyzerSurface_EmitsTypedIndex_AndThreeReadPaths()
    {
        var result = ParserGeneratorTestHarness.Run(OneAnalyzer("[ForPull(PullKind.Single)]"));
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain("private readonly global::FellowshipAnalyzer.Core.Analysis.PullAnalyzerList<global::Test.ComboAnalyzer> _comboAnalyzers = new();");
        gen.ShouldContain("public global::System.Collections.Generic.IReadOnlyList<global::FellowshipAnalyzer.Core.Analysis.PullAnalyzer<global::Test.ComboAnalyzer>> ComboAnalyzers => ClampToSelectedPull(_comboAnalyzers);");

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
    public void HeroParser_IsRegisteredTransient_SoEachAnalysisGetsItsOwnInstance()
    {
        var result = ParserGeneratorTestHarness.Run(HeroParser());
        var gen = result.ConcatenatedGenerated;

        gen.ShouldContain("services.AddTransient<ComboCombatLogParser>();");
        gen.ShouldContain("services.AddKeyedTransient<IHeroAnalyzer>(global::FellowshipAnalyzer.Core.Analysis.HeroName.Rime, (sp, _) => sp.GetRequiredService<ComboCombatLogParser>());");
        gen.ShouldNotContain("AddScoped");
        gen.ShouldNotContain("AddKeyedScoped");
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
    public void TwoAnalyzers_SharedSurfaceInterface_ShareOneSurfaceStream()
    {
        var result = ParserGeneratorTestHarness.Run(TwoAnalyzersSharedInterface());
        var gen = result.ConcatenatedGenerated;

        OccurrenceCount(gen, "_comboAnalyzers = new();").ShouldBe(1);
        OccurrenceCount(gen, "case global::Test.IComboAnalyzer").ShouldBe(1);
        gen.ShouldContain("public global::System.Collections.Generic.IReadOnlyList<global::FellowshipAnalyzer.Core.Analysis.PullAnalyzer<global::Test.IComboAnalyzer>> ComboAnalyzers => ClampToSelectedPull(_comboAnalyzers);");
        gen.ShouldContain("public global::Test.IComboAnalyzer? ComboAnalyzer => (global::Test.IComboAnalyzer?)pull.GetAnalyzer(typeof(global::Test.IComboAnalyzer));");
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

    private static string HeroParser() => Usings + """

        namespace Test;

        [HeroAnalyzer(HeroName.Rime)]
        [AddAnalyzer<ComboAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        [ForPull(PullKind.Single)]
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

    private static string TwoAnalyzersSharedInterface() => Usings + """

        namespace Test;

        [AddAnalyzer<StAnalyzer>]
        [AddAnalyzer<AoeAnalyzer>]
        public sealed partial class ComboCombatLogParser : CombatLogParser { }

        public interface IComboAnalyzer : IAnalyzerSurface;

        [ForPull(PullKind.Single)]
        public sealed partial class StAnalyzer : Analyzer, IComboAnalyzer
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
        }

        [ForPull(PullKind.Multi)]
        public sealed partial class AoeAnalyzer : Analyzer, IComboAnalyzer
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { }
        }
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
        [Dependency<DepModule>]
        public sealed partial class ConsumerAnalyzer : Analyzer
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { _ = DepModule.Ping() + DepModule.Beep(); }
        }
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
        [Dependency<DepModule>]
        public sealed partial class ConflictAnalyzer(Lazy<OtherDep> other) : Analyzer
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { _ = _other.Ping(); }
        }
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
        [Dependency<OtherDep>]
        [Dependency<DepModule>]
        public sealed partial class MultiAnalyzer : Analyzer
        {
            [On<ApplyBuffEvent>]
            private void OnBuff(ApplyBuffEvent e) { _ = DepModule.Ping() + OtherDep.Pong(); }
        }
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

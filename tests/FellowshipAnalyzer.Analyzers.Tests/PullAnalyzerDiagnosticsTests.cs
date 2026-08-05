using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Analyzers.Tests;

public class PullAnalyzerDiagnosticsTests
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        """;

    [Fact]
    public void FA0014_AnalyzerDependingOnPullAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<DepAnalyzer>]
            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class DepAnalyzer : Analyzer { }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer(DepAnalyzer dep) : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0014");
    }

    [Fact]
    public void FA0014_LazyPullAnalyzerDependency_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<DepAnalyzer>]
            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class DepAnalyzer : Analyzer { }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer(Lazy<DepAnalyzer> dep) : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0014");
    }

    [Fact]
    public void FA0014_DependencyAttributeOnPullAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<DepAnalyzer>]
            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class DepAnalyzer : Analyzer { }

            [ForPull(PullKind.Single)]
            [Dependency<DepAnalyzer>]
            public sealed class MyAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0014");
    }

    /// <summary>
    /// FA0014 is not analyzer-on-analyzer: a plain module cannot reach a per-pull instance either, and
    /// throws the same "No registered module is assignable" at runtime.
    /// </summary>
    [Fact]
    public void FA0014_ModuleDependingOnPullAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddModule<MyModule>]
            [AddAnalyzer<DepAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class DepAnalyzer : Analyzer { }

            public sealed class MyModule(Lazy<DepAnalyzer> dep) : Module { }
            """);

        Ids(diagnostics).ShouldContain("FA0014");
    }

    [Fact]
    public void FA0014_NormalizerDependingOnPullAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddNormalizer<MyNormalizer>]
            [AddAnalyzer<DepAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class DepAnalyzer : Analyzer { }

            public sealed class MyNormalizer(Lazy<DepAnalyzer> dep) : IEventNormalizer
            {
                public int Priority => 0;
                public List<Event> Normalize(List<Event> events, int playerId) => events;
            }
            """);

        Ids(diagnostics).ShouldContain("FA0014");
    }

    /// <summary>
    /// An analyzer without <c>[ForPull]</c> is parse-lifetime, resolved from <c>GetModuleTypes()</c> like
    /// any module, so depending on it is exactly what FA0014 leaves alone.
    /// </summary>
    [Fact]
    public void FA0014_DependingOnParseLifetimeAnalyzer_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<MyState>]
            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            public sealed class MyState : Analyzer { }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer(MyState state) : Analyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0014");
    }

    [Fact]
    public void FA0015_EmptyTargets_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<NoTargetAnalyzer>]
            public abstract class Host { }

            [ForPull((PullKind)0)]
            public sealed class NoTargetAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0015");
    }

    /// <summary>
    /// A registered analyzer with no <c>[ForPull]</c> is a parse-lifetime analyzer, which is what most of
    /// <c>CombatLogParser</c>'s own registrations are.
    /// </summary>
    [Fact]
    public void FA0015_MissingForPull_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<ParseLifetimeAnalyzer>]
            public abstract class Host { }

            public sealed class ParseLifetimeAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0015");
    }

    [Fact]
    public void FA0015_PresentForPull_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0015");
    }

    [Fact]
    public void FA0016_OverlappingFilters_SharedSurface_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<WideAnalyzer>]
            [AddAnalyzer<StAnalyzer>]
            public abstract class Host { }

            public abstract class ComboAnalyzer : Analyzer { }

            [ForPull(PullKind.Single | PullKind.Multi)]
            public sealed class WideAnalyzer : ComboAnalyzer { }

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : ComboAnalyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0016");
    }

    [Fact]
    public void FA0016_DisjointTargets_SharedSurface_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<StAnalyzer>]
            [AddAnalyzer<AoeAnalyzer>]
            public abstract class Host { }

            public abstract class ComboAnalyzer : Analyzer { }

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : ComboAnalyzer { }

            [ForPull(PullKind.Multi)]
            public sealed class AoeAnalyzer : ComboAnalyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    [Fact]
    public void FA0016_DisjointBoss_SharedSurface_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<BossAnalyzer>]
            [AddAnalyzer<TrashAnalyzer>]
            public abstract class Host { }

            public abstract class ComboAnalyzer : Analyzer { }

            [ForPull(PullKind.Single, Boss = PullBoss.Boss)]
            public sealed class BossAnalyzer : ComboAnalyzer { }

            [ForPull(PullKind.Single, Boss = PullBoss.NonBoss)]
            public sealed class TrashAnalyzer : ComboAnalyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    [Fact]
    public void FA0016_DistinctSurfaces_OverlappingFilters_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<A1>]
            [AddAnalyzer<A2>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class A1 : Analyzer { }

            [ForPull(PullKind.Single)]
            public sealed class A2 : Analyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    /// <summary>
    /// Two parse-lifetime analyzers share the <c>Analyzer</c> surface trivially and are constructed once
    /// each, so FA0016 has nothing to say about them.
    /// </summary>
    [Fact]
    public void FA0016_ParseLifetimeAnalyzers_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<StateA>]
            [AddAnalyzer<StateB>]
            public abstract class Host { }

            public abstract class SharedBase : Analyzer { }

            public sealed class StateA : SharedBase { }

            public sealed class StateB : SharedBase { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    [Fact]
    public void FA0016_OverlappingFilters_SharedInterface_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<WideAnalyzer>]
            [AddAnalyzer<StAnalyzer>]
            public abstract class Host { }

            public interface IComboAnalyzer : IAnalyzerSurface;

            [ForPull(PullKind.Single | PullKind.Multi)]
            public sealed class WideAnalyzer : Analyzer, IComboAnalyzer { }

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : Analyzer, IComboAnalyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0016");
    }

    [Fact]
    public void FA0016_DisjointTargets_SharedInterface_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<StAnalyzer>]
            [AddAnalyzer<AoeAnalyzer>]
            public abstract class Host { }

            public interface IComboAnalyzer : IAnalyzerSurface;

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : Analyzer, IComboAnalyzer { }

            [ForPull(PullKind.Multi)]
            public sealed class AoeAnalyzer : Analyzer, IComboAnalyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    [Fact]
    public void FA0017_MultipleSurfaceInterfaces_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<BadAnalyzer>]
            public abstract class Host { }

            public interface IFooAnalyzer : IAnalyzerSurface;
            public interface IBarAnalyzer : IAnalyzerSurface;

            [ForPull(PullKind.Single)]
            public sealed class BadAnalyzer : Analyzer, IFooAnalyzer, IBarAnalyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0017");
    }

    [Fact]
    public void FA0017_SingleSurfaceInterface_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            public interface IComboAnalyzer : IAnalyzerSurface;

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer : Analyzer, IComboAnalyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0017");
    }

    [Fact]
    public void FA0019_AddModuleOfAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddModule<MyAnalyzer>]
            public abstract class Host { }

            public sealed class MyAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0019");
    }

    [Fact]
    public void FA0019_AddStateOfAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddState<MyAnalyzer>]
            public abstract class Host { }

            public sealed class MyAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0019");
    }

    [Fact]
    public void FA0019_AddModuleOfModule_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddModule<MyModule>]
            public abstract class Host { }

            public sealed class MyModule : Module { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0019");
    }

    [Fact]
    public void FA0020_ForPullOnNonAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [ForPull(PullKind.Single)]
            public sealed class NotAnAnalyzer : Module { }
            """);

        Ids(diagnostics).ShouldContain("FA0020");
    }

    [Fact]
    public void FA0020_ForPullOnAnalyzer_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0020");
    }

    [Fact]
    public void FA0021_ForPullOnAbstractAnalyzer_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [ForPull(PullKind.Single)]
            public abstract class BaseAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0021");
    }

    /// <summary>
    /// The shape an abstract base is allowed to have: it declares the members, and each concrete subclass
    /// declares its own <c>[ForPull]</c>.
    /// </summary>
    [Fact]
    public void FA0021_AbstractBaseWithoutForPull_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            public abstract class BaseAnalyzer : Analyzer { }

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : BaseAnalyzer { }

            [ForPull(PullKind.Multi)]
            public sealed class AoeAnalyzer : BaseAnalyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0021");
    }

    private static IEnumerable<string> Ids(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Select(d => d.Id);
}

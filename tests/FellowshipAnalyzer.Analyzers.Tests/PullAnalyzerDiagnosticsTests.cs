using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Analyzers.Tests;

public class PullAnalyzerDiagnosticsTests
{
    private const string Usings = """
        using System;
        using FellowshipAnalyzer.Core.Analysis;
        """;

    [Fact]
    public void FA0014_AnalyzerDependingOnAnalyzer_Reports()
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
    public void FA0014_LazyAnalyzerDependency_Reports()
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
    public void FA0014_DependingOnState_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddState<MyState>]
            [AddAnalyzer<MyAnalyzer>]
            public abstract class Host { }

            public sealed class MyState : EventSubscriber { }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer(MyState state) : Analyzer { }
            """);

        Ids(diagnostics).ShouldNotContain("FA0014");
    }

    [Fact]
    public void FA0015_MissingForPull_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<NoFilterAnalyzer>]
            public abstract class Host { }

            public sealed class NoFilterAnalyzer : Analyzer { }
            """);

        Ids(diagnostics).ShouldContain("FA0015");
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

    private static IEnumerable<string> Ids(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Select(d => d.Id);
}

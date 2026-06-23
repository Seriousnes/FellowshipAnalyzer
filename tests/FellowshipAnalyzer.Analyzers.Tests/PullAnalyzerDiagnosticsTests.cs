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
            public sealed class DepAnalyzer : Analyzer<DepResult>
            {
                public override DepResult OnPullEnd() => new();
            }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer(DepAnalyzer dep) : Analyzer<MyResult>
            {
                public override MyResult OnPullEnd() => new();
            }

            public sealed record DepResult : IResult;
            public sealed record MyResult : IResult;
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
            public sealed class DepAnalyzer : Analyzer<DepResult>
            {
                public override DepResult OnPullEnd() => new();
            }

            [ForPull(PullKind.Single)]
            public sealed class MyAnalyzer(Lazy<DepAnalyzer> dep) : Analyzer<MyResult>
            {
                public override MyResult OnPullEnd() => new();
            }

            public sealed record DepResult : IResult;
            public sealed record MyResult : IResult;
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
            public sealed class MyAnalyzer(MyState state) : Analyzer<MyResult>
            {
                public override MyResult OnPullEnd() => new();
            }

            public sealed record MyResult : IResult;
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

            public sealed class NoFilterAnalyzer : Analyzer<MyResult>
            {
                public override MyResult OnPullEnd() => new();
            }

            public sealed record MyResult : IResult;
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
            public sealed class MyAnalyzer : Analyzer<MyResult>
            {
                public override MyResult OnPullEnd() => new();
            }

            public sealed record MyResult : IResult;
            """);

        Ids(diagnostics).ShouldNotContain("FA0015");
    }

    [Fact]
    public void FA0016_OverlappingFilters_SameResult_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<WideAnalyzer>]
            [AddAnalyzer<StAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single | PullKind.Multi)]
            public sealed class WideAnalyzer : Analyzer<ComboResult>
            {
                public override ComboResult OnPullEnd() => new();
            }

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : Analyzer<ComboResult>
            {
                public override ComboResult OnPullEnd() => new();
            }

            public sealed record ComboResult : IResult;
            """);

        Ids(diagnostics).ShouldContain("FA0016");
    }

    [Fact]
    public void FA0016_DisjointTargets_SameResult_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<StAnalyzer>]
            [AddAnalyzer<AoeAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class StAnalyzer : Analyzer<ComboResult>
            {
                public override ComboResult OnPullEnd() => new();
            }

            [ForPull(PullKind.Multi)]
            public sealed class AoeAnalyzer : Analyzer<ComboResult>
            {
                public override ComboResult OnPullEnd() => new();
            }

            public sealed record ComboResult : IResult;
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    [Fact]
    public void FA0016_DisjointBoss_SameResult_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<BossAnalyzer>]
            [AddAnalyzer<TrashAnalyzer>]
            public abstract class Host { }

            [ForPull(PullKind.Single, Boss = PullBoss.Boss)]
            public sealed class BossAnalyzer : Analyzer<ComboResult>
            {
                public override ComboResult OnPullEnd() => new();
            }

            [ForPull(PullKind.Single, Boss = PullBoss.NonBoss)]
            public sealed class TrashAnalyzer : Analyzer<ComboResult>
            {
                public override ComboResult OnPullEnd() => new();
            }

            public sealed record ComboResult : IResult;
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    [Fact]
    public void FA0016_DifferentResults_OverlappingFilters_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Usings + """

            namespace Test;

            [AddAnalyzer<A1>]
            [AddAnalyzer<A2>]
            public abstract class Host { }

            [ForPull(PullKind.Single)]
            public sealed class A1 : Analyzer<ResultOne>
            {
                public override ResultOne OnPullEnd() => new();
            }

            [ForPull(PullKind.Single)]
            public sealed class A2 : Analyzer<ResultTwo>
            {
                public override ResultTwo OnPullEnd() => new();
            }

            public sealed record ResultOne : IResult;
            public sealed record ResultTwo : IResult;
            """);

        Ids(diagnostics).ShouldNotContain("FA0016");
    }

    private static IEnumerable<string> Ids(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Select(d => d.Id);
}

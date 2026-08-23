using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Analyzers.Tests;

public class OnHandlerSignatureAnalyzerTests
{
    private const string Usings = """
        using System.Threading.Tasks;
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        """;

    private static IEnumerable<string> Ids(
        System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(d => d.Id);

    private static IEnumerable<string> Run(string body) =>
        Ids(AnalyzerTestHarness.Run(Usings + body, new OnHandlerSignatureAnalyzer()));

    [Fact]
    public void NoParam_Silent() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private void OnCast() { }
            }
            """).ShouldNotContain("FA0011");

    [Fact]
    public void NoParam_Async_Silent() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private Task OnCast() => Task.CompletedTask;
            }
            """).ShouldNotContain("FA0011");

    [Fact]
    public void ConcreteParam_Silent() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private void OnCast(CastEvent e) { }
            }
            """).ShouldNotContain("FA0011");

    [Fact]
    public void TwoParams_Reports() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private void OnCast(CastEvent e, int extra) { }
            }
            """).ShouldContain("FA0011");

    [Fact]
    public void UnrelatedParamType_Reports() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private void OnCast(DeathEvent e) { }
            }
            """).ShouldContain("FA0011");

    [Fact]
    public void Param_ValueTaskReturning_Silent() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private ValueTask OnCast(CastEvent e) => default;
            }
            """).ShouldNotContain("FA0011");

    [Fact]
    public void NoParam_ValueTaskReturning_Silent() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private ValueTask OnCast() => default;
            }
            """).ShouldNotContain("FA0011");

    [Fact]
    public void UnrelatedTypeNamedTaskReturn_Reports() =>
        Run("""

            namespace Unrelated
            {
                public sealed class Task { }
            }

            namespace Test
            {
                public sealed class MyAnalyzer : Analyzer
                {
                    [On<CastEvent>]
                    private global::Unrelated.Task OnCast(CastEvent e) => null!;
                }
            }
            """).ShouldContain("FA0011");

    [Fact]
    public void NonAwaitableReturn_Reports() =>
        Run("""

            namespace Test;
            public sealed class MyAnalyzer : Analyzer
            {
                [On<CastEvent>]
                private int OnCast() => 0;
            }
            """).ShouldContain("FA0011");
}

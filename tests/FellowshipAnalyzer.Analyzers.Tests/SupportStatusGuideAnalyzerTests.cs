using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Analyzers.Tests;

public class SupportStatusGuideAnalyzerTests
{
    private const string Preamble = """
        using FellowshipAnalyzer.Core.Analysis;

        namespace Test;

        public class FakeParser { public virtual System.Type? GuideComponent => null; }
        """;

    private static System.Collections.Generic.IEnumerable<string> Ids(
        System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(d => d.Id);

    [Fact]
    public void FA0017_MaintainedWithoutGuide_Reports()
    {
        var diagnostics = AnalyzerTestHarness.Run(Preamble + """

            [HeroAnalyzer(HeroName.Helena)]
            public partial class HelenaParser : FakeParser
            {
                public static HeroConfig HeroConfig { get; } = new() { Support = SupportLevel.Partial };
            }
            """, new SupportStatusGuideAnalyzer());

        Ids(diagnostics).ShouldContain("FA0017");
    }

    [Fact]
    public void FA0017_MaintainedWithGuide_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Preamble + """

            [HeroAnalyzer(HeroName.Rime)]
            public partial class RimeParser : FakeParser
            {
                public override System.Type? GuideComponent => typeof(string);
                public static HeroConfig HeroConfig { get; } = new() { Support = SupportLevel.Full };
            }
            """, new SupportStatusGuideAnalyzer());

        Ids(diagnostics).ShouldNotContain("FA0017");
    }

    [Fact]
    public void FA0017_NoSupportWithoutGuide_Silent()
    {
        var diagnostics = AnalyzerTestHarness.Run(Preamble + """

            [HeroAnalyzer(HeroName.Meiko)]
            public partial class MeikoParser : FakeParser
            {
                public static HeroConfig HeroConfig { get; } = new() { Support = SupportLevel.None };
            }
            """, new SupportStatusGuideAnalyzer());

        Ids(diagnostics).ShouldNotContain("FA0017");
    }
}

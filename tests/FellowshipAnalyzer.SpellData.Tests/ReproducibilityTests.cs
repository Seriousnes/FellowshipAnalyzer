using FellowshipAnalyzer.SpellData;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class ReproducibilityTests
{
    [Fact]
    public void CommittedSpellDb_EqualsFreshMerge()
    {
        var fresh = SpellDbWriter.Serialize(MergeEngine.Run(MergeInputs.Load()));
        var committed = File.ReadAllText(SourcePaths.SpellDb).Replace("\r\n", "\n");
        committed.ShouldBe(fresh.Replace("\r\n", "\n"));
    }
}

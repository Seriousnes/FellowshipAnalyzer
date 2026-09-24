using FellowshipAnalyzer.SpellData;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class LegendaryTests
{
    [Fact]
    public void Run_JoinsEveryLegendaryItemToThePowerItGrantsAndTheHeroThatEquipsIt()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        var aeona = result.Legendaries.Where(l => l.Scope == "aeona").ToList();

        aeona.Select(l => (l.Member, l.ItemId, l.PowerId, l.Slot)).ShouldBe(
        [
            ("ChronoTrigger", 5221, 618, "Ring"),
            ("LonesomeSong", 5217, 619, "Back"),
            ("MassEntropy", 5226, 620, "Wrists"),
        ]);
        aeona.Single(l => l.Member == "MassEntropy").PowerName.ShouldBe("Mass Entropy");
        aeona.Single(l => l.Member == "MassEntropy").ItemName.ShouldBe("Bands of the Withering Shores");
    }

    [Fact]
    public void Run_NamesEveryLegendaryAsAValidIdentifierWithoutCollidingInsideAHero()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Legendaries.ShouldNotBeEmpty();
        result.Legendaries.ShouldAllBe(l => MemberNaming.IsValidIdentifier(l.Member));
        result.Legendaries.GroupBy(l => (l.Scope, l.Member)).ShouldAllBe(g => g.Count() == 1);
    }

    [Fact]
    public void Deserialize_RoundTripsTheLegendariesSection()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.Legendaries.Select(l => (l.Scope, l.Member, l.ItemId, l.PowerId, l.PowerName, l.Slot, l.Icon))
            .ShouldBe(original.Legendaries.Select(l => (l.Scope, l.Member, l.ItemId, l.PowerId, l.PowerName, l.Slot, l.Icon)), ignoreOrder: true);
    }
}

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
            ("ChronoTrigger", 5898, 618, "Chest"),
            ("LonesomeSong", 5899, 619, "Head"),
            ("MassEntropy", 5900, 620, "Legs"),
        ]);
        aeona.Single(l => l.Member == "MassEntropy").PowerName.ShouldBe("Mass Entropy");
        aeona.Single(l => l.Member == "MassEntropy").ItemName.ShouldBe("Timeweaver Leggings");
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

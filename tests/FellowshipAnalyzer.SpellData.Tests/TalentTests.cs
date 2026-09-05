using FellowshipAnalyzer.SpellData;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class TalentTests
{
    [Theory]
    [InlineData("Assassin's Guile", "AssassinsGuile")]
    [InlineData("Sword & Board", "SwordAndBoard")]
    [InlineData("Striker's Aim", "StrikersAim")]
    [InlineData("Maiden's Doom", "MaidensDoom")]
    [InlineData("Blood & Thunder", "BloodAndThunder")]
    public void TalentMember_KeepsTheLettersAfterAnApostropheAndReadsAmpersandAsAnd(string name, string expected) =>
        MemberNaming.TalentMember(name).ShouldBe(expected);

    [Fact]
    public void Run_SlotsEveryTalentTheExportDeclaresToItsHero()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Talents.Count.ShouldBe(216);
        result.Talents.Select(t => t.Scope).Distinct().Count().ShouldBe(12);
        result.Talents.GroupBy(t => t.Scope).ShouldAllBe(hero => hero.Count() == 18);
    }

    [Fact]
    public void Run_TakesMacabreStratagemsIdFromTheExport()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        var talent = result.Talents.Single(t => t.Scope == "mara" && t.Member == "MacabreStratagem");
        talent.Spell.Id.ShouldBe(8);
    }

    [Fact]
    public void Run_NamesEveryTalentAsAValidIdentifierWithoutCollidingInsideAHero()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Talents.ShouldAllBe(t => MemberNaming.IsValidIdentifier(t.Member));
        result.Talents.GroupBy(t => (t.Scope, t.Member)).ShouldAllBe(g => g.Count() == 1);
    }

    [Fact]
    public void Run_GivesEveryTalentAnIcon() =>
        MergeEngine.Run(MergeInputs.Load()).Talents.ShouldAllBe(t => t.Spell.Icon.Length > 0);

    [Fact]
    public void Deserialize_RoundTripsTheTalentsSection()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.Talents.Count.ShouldBe(original.Talents.Count);
        restored.Talents.Select(t => (t.Scope, t.Member, t.Spell.Id))
            .ShouldBe(original.Talents.Select(t => (t.Scope, t.Member, t.Spell.Id)), ignoreOrder: true);
    }
}

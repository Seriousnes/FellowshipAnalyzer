using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class TuningTests
{
    private static readonly MergeResult Merged = MergeEngine.Run(MergeInputs.Load());

    private static TuningRow Row(string scope, string container, string member, string key) =>
        Merged.Tuning.Single(row => row.Scope == scope && row.Container == container && row.Member == member && row.Key == key);

    private static void ShouldBeNumber(TuningRow row, double expected)
    {
        row.Value.Number.ShouldBe(expected);
        row.Value.Numbers.ShouldBeNull();
    }

    private static void ShouldBeNumbers(TuningRow row, params double[] expected)
    {
        row.Value.Number.ShouldBeNull();
        row.Value.Numbers.ShouldBe(expected);
    }

    [Fact]
    public void Ardeos_FireBall_ReducesTheApocalypseCooldownByEightSeconds() =>
        ShouldBeNumber(Row("ardeos", "spells", "FireBall", "CooldownReductionToApocalypse.ReductionInSeconds"), 8);

    [Fact]
    public void Ardeos_Apocalypse_HoldsTwoChargesAtMost() =>
        ShouldBeNumber(Row("ardeos", "spells", "Apocalypse", "AddedCharges.MaxCharges"), 2);

    [Fact]
    public void Ardeos_EngulfingFlames_ReducesItsCooldownByTenSecondsOnTargetDeath() =>
        ShouldBeNumber(Row("ardeos", "spells", "EngulfingFlames", "CooldownReductionInSecondsOnTargetDeath"), 10);

    [Fact]
    public void Gunde_HeartSplitter_HasTwoChargesWithTheLegendary() =>
        ShouldBeNumber(Row("gunde", "spells", "HeartSplitter", "Charges.NumOfCharges"), 2);

    [Fact]
    public void Rime_ColdSnap_ReducesItsCooldownByOnePointFiveSecondsOnTheChanneledBeam() =>
        ShouldBeNumber(Row("rime", "spells", "ColdSnap", "ReduceCooldownOnChanneledBeam.ReductionInSeconds"), 1.5);

    [Fact]
    public void Rime_ColdSnap_HoldsTwoCharges() =>
        ShouldBeNumber(Row("rime", "spells", "ColdSnap", "MaxCharges"), 2);

    [Fact]
    public void Items_Chronoshift_AddsEightCooldownRecoveryWhileActive() =>
        ShouldBeNumber(Row("items", "spells", "Chronoshift", "Channel.WhileActiveAddedCooldownRecovery"), 8);

    [Fact]
    public void Items_RepositoryOfFrozenLight_ReducesCooldownsByThreeTenthsOfASecondPerStack() =>
        ShouldBeNumber(Row("items", "spells", "RepositoryOfFrozenLight", "CooldownReductionInSecondsPerStack"), 0.3);

    [Fact]
    public void Glyphs_TheMonarch_StatesFourRanksOfCooldownAcceleration() =>
        ShouldBeNumbers(Row("glyphs", "glyphs", "TheMonarch", "CooldownAcceleration"), 0.03, 0.05, 0.08, 0.12);

    [Fact]
    public void Glyphs_TheMonarch_StatesFourRanksOfBonusIncreasePerHaste() =>
        ShouldBeNumbers(Row("glyphs", "glyphs", "TheMonarch", "BonusIncreasePerHasteInPercentage"), 0.1, 0.1, 0.1, 0.1);

    [Fact]
    public void Glyphs_TheMonarch_StatesFourRanksOfHasteThreshold() =>
        ShouldBeNumbers(Row("glyphs", "glyphs", "TheMonarch", "HasteThreshold"), 0.5, 0.5, 0.5, 0.5);

    [Fact]
    public void EveryTuningRow_BelongsToAMemberTheMergeSelected()
    {
        var spells = Merged.Spells.Select(spell => (spell.Scope, spell.Member)).ToHashSet();
        var talents = Merged.Talents.Select(talent => (talent.Scope, talent.Member)).ToHashSet();
        var legendaries = Merged.Legendaries.Select(legendary => (legendary.Scope, legendary.Member)).ToHashSet();

        Merged.Tuning.ShouldNotBeEmpty();
        foreach (var row in Merged.Tuning)
        {
            var selected = row.Container switch
            {
                "spells" => spells.Contains((row.Scope, row.Member)),
                "talents" => talents.Contains((row.Scope, row.Member)),
                "legendaries" => legendaries.Contains((row.Scope, row.Member)),
                "glyphs" => row.Scope == "glyphs",
                _ => false,
            };

            selected.ShouldBeTrue($"{row.Scope}.{row.Container}.{row.Member}");
        }
    }

    [Fact]
    public void Gunde_Carnage_StatesItsCooldownReductionPerHit()
    {
        Merged.Talents.Single(talent => talent.Scope == "gunde" && talent.Member == "Carnage")
            .Spell.FSLID.NativeId.ShouldBe(754);

        var row = Merged.Tuning
            .Where(row => row.Scope == "gunde" && row.Container == "talents" && row.Member == "Carnage"
                && row.Key.EndsWith("CooldownReductionInSecondsPerHit", StringComparison.Ordinal))
            .ShouldHaveSingleItem();

        ShouldBeNumber(row, 1);
    }

    [Fact]
    public void NoTuningRow_HasANullValue() =>
        Merged.Tuning.ShouldAllBe(row => (row.Value.Number != null) != (row.Value.Numbers != null));

    [Fact]
    public void EveryTuningRow_HasAUniqueGeneratedIdentifierWithinItsMember()
    {
        foreach (var member in Merged.Tuning.GroupBy(row => (row.Scope, row.Container, row.Member)))
        {
            var identifiers = member.Select(row => MemberNaming.TuningIdentifier(row.Key)).ToList();
            identifiers.ShouldBeUnique($"{member.Key}");
            identifiers.ShouldNotContain(member.Key.Member);
            identifiers.ShouldAllBe(identifier => identifier.Length > 0);
        }
    }

    [Theory]
    [InlineData("CooldownReductionToApocalypse.ReductionInSeconds", "CooldownReductionToApocalypse_ReductionInSeconds")]
    [InlineData("2ndHit.Damage", "_2ndHit_Damage")]
    [InlineData("Heal-Over Time", "HealOverTime")]
    [InlineData("MaxCharges", "MaxCharges")]
    public void TuningIdentifier_ReplacesDotsAndDropsOtherCharacters(string key, string expected) =>
        MemberNaming.TuningIdentifier(key).ShouldBe(expected);

    [Fact]
    public void SerializedTuning_RoundTripsThroughTheWriter()
    {
        var read = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(Merged));

        read.Tuning.Count.ShouldBe(Merged.Tuning.Count);
        foreach (var (written, parsed) in Merged.Tuning.Zip(read.Tuning))
        {
            parsed.Scope.ShouldBe(written.Scope);
            parsed.Container.ShouldBe(written.Container);
            parsed.Member.ShouldBe(written.Member);
            parsed.Key.ShouldBe(written.Key);
            parsed.Value.Number.ShouldBe(written.Value.Number);
            parsed.Value.Numbers.ShouldBe(written.Value.Numbers);
        }
    }
}

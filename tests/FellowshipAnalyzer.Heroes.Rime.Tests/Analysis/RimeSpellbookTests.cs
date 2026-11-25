using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using Xunit;

using RimeAbilities = FellowshipAnalyzer.Heroes.Rime.Modules.Abilities;
using RimeSpells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Analysis;

public sealed class RimeSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new RimeAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public void NoEntry_OverridesTheGlobalCooldownOfItsSpellData() =>
        Assert.DoesNotContain(new RimeAbilities().Spellbook(), entry => entry.Gcd is not null);

    [Fact]
    public void WrathOfWinterAndFrostSwallows_HaveNoGlobalCooldown()
    {
        var spellbook = new RimeAbilities().Spellbook().ToList();

        Assert.False(spellbook.Single(e => e.PrimarySpell.Id == RimeSpells.WrathOfWinter.Id).OnGlobalCooldown);
        Assert.False(spellbook.Single(e => e.PrimarySpell.Id == RimeSpells.FrostSwallows.Id).OnGlobalCooldown);
    }

    [Fact]
    public void ColdSnap_IsOnTheGlobalCooldown() =>
        Assert.True(new RimeAbilities().Spellbook().Single(e => e.PrimarySpell.Id == RimeSpells.ColdSnap.Id).OnGlobalCooldown);

    [Fact]
    public void FreezingTorrent_ComposesDataScalarsWithBehaviour()
    {
        var entry = new RimeAbilities().Spellbook()
            .Single(e => e.PrimarySpell.Id == RimeSpells.FreezingTorrent.Id);

        Assert.Equal(SpellCategory.Rotational, entry.Category);
        Assert.True(entry.OnGlobalCooldown);
        Assert.Equal(15, entry.PrimarySpell.Cooldown);
        Assert.Equal(2.0, entry.ChannelDuration);
        Assert.Equal(0.4, entry.ChannelTickInterval);
    }

    [Fact]
    public void ColdSnap_UsesTheDataCooldownWithHasteOnItsTimeRate()
    {
        var entry = new RimeAbilities().Spellbook()
            .Single(e => e.PrimarySpell.Id == RimeSpells.ColdSnap.Id);

        Assert.Equal(12, entry.PrimarySpell.Cooldown);
        Assert.Equal(2, entry.Charges);
        Assert.Equal(CooldownHasteScaling.TimeRate, entry.CooldownHaste);
        Assert.True(entry.CooldownReducedByHaste);
        Assert.Equal(6, entry.GetCooldown(haste: 1.0));
    }
}

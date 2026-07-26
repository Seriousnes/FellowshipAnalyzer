using FellowshipAnalyzer.Core.Analysis;

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
    public void FreezingTorrent_ComposesDataScalarsWithBehaviour()
    {
        var entry = new RimeAbilities().Spellbook()
            .Single(e => e.PrimarySpell.Id == RimeSpells.FreezingTorrent.Id);

        Assert.Equal(SpellCategory.Rotational, entry.Category);
        Assert.NotNull(entry.Gcd);
        Assert.Equal(15, entry.PrimarySpell.Cooldown);
        Assert.Equal(2.0, entry.ChannelDuration);
        Assert.Equal(0.4, entry.ChannelTickInterval);
    }

    [Fact]
    public void ColdSnap_UsesTheDataCooldownAndIsNotReducedByHaste()
    {
        var entry = new RimeAbilities().Spellbook()
            .Single(e => e.PrimarySpell.Id == RimeSpells.ColdSnap.Id);

        Assert.Equal(12, entry.PrimarySpell.Cooldown);
        Assert.Equal(2, entry.Charges);
        Assert.False(entry.CooldownReducedByHaste);
        Assert.Equal(12, entry.GetCooldown(haste: 1.0));
    }
}

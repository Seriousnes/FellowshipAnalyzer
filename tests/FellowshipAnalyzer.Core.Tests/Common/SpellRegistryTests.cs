using FellowshipAnalyzer.Core.Common.Spells;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

public class SpellRegistryTests
{
    [Fact]
    public void Get_KnownSpell_ReturnsCorrectSpell()
    {
        var spell = SpellRegistry.Get(RimeSpells.FrostBolt.Id);

        Assert.Equal(RimeSpells.FrostBolt.Id, spell.Id);
        Assert.Equal(RimeSpells.FrostBolt.Name, spell.Name);
    }

    [Fact]
    public void Get_UnknownId_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => SpellRegistry.Get(-99999));
    }

    [Fact]
    public void MaybeGet_KnownSpell_ReturnsSpell()
    {
        var spell = SpellRegistry.MaybeGet(RimeSpells.GlacialBlast.Id);

        Assert.NotNull(spell);
        Assert.Equal(RimeSpells.GlacialBlast.Name, spell.Name);
    }

    [Fact]
    public void MaybeGet_UnknownId_ReturnsNull()
    {
        var spell = SpellRegistry.MaybeGet(-99999);

        Assert.Null(spell);
    }

    [Fact]
    public void TryGet_KnownSpell_ReturnsTrueAndSpell()
    {
        var found = SpellRegistry.TryGet(RimeSpells.IceComet.Id, out var spell);

        Assert.True(found);
        Assert.NotNull(spell);
        Assert.Equal(RimeSpells.IceComet.Name, spell.Name);
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsFalseAndNull()
    {
        var found = SpellRegistry.TryGet(-99999, out var spell);

        Assert.False(found);
        Assert.Null(spell);
    }

    [Fact]
    public void All_ContainsAllRimeSpells()
    {
        var allIds = SpellRegistry.All.Keys;

        Assert.Contains(RimeSpells.FrostBolt.Id, allIds);
        Assert.Contains(RimeSpells.ColdSnap.Id, allIds);
        Assert.Contains(RimeSpells.FreezingTorrent.Id, allIds);
        Assert.Contains(RimeSpells.BurstingIce.Id, allIds);
        Assert.Contains(RimeSpells.GlacialBlast.Id, allIds);
        Assert.Contains(RimeSpells.IceComet.Id, allIds);
    }
}

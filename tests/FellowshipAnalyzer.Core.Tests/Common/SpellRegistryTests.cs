using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

public class SpellRegistryTests
{
    [Fact]
    public void Get_KnownSpell_ReturnsCorrectSpell()
    {
        var spell = SpellRegistry.Get(Core.Common.Spells.Rime.Spells.FrostBolt.Id);

        Assert.Equal(Core.Common.Spells.Rime.Spells.FrostBolt.Id, spell.Id);
        Assert.Equal(Core.Common.Spells.Rime.Spells.FrostBolt.Name, spell.Name);
    }

    [Fact]
    public void Get_UnknownId_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => SpellRegistry.Get(-99999));
    }

    [Fact]
    public void MaybeGet_KnownSpell_ReturnsSpell()
    {
        var spell = SpellRegistry.MaybeGet(Core.Common.Spells.Rime.Spells.GlacialBlast.Id);

        Assert.NotNull(spell);
        Assert.Equal(Core.Common.Spells.Rime.Spells.GlacialBlast.Name, spell.Name);
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
        var found = SpellRegistry.TryGet(Core.Common.Spells.Rime.Spells.IceComet.Id, out var spell);

        Assert.True(found);
        Assert.NotNull(spell);
        Assert.Equal(Core.Common.Spells.Rime.Spells.IceComet.Name, spell.Name);
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

        Assert.Contains(Core.Common.Spells.Rime.Spells.FrostBolt.Id, allIds);
        Assert.Contains(Core.Common.Spells.Rime.Spells.ColdSnap.Id, allIds);
        Assert.Contains(Core.Common.Spells.Rime.Spells.FreezingTorrent.Id, allIds);
        Assert.Contains(Core.Common.Spells.Rime.Spells.BurstingIce.Id, allIds);
        Assert.Contains(Core.Common.Spells.Rime.Spells.GlacialBlast.Id, allIds);
        Assert.Contains(Core.Common.Spells.Rime.Spells.IceComet.Id, allIds);
    }
}

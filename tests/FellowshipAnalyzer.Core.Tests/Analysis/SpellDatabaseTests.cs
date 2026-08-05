using FellowshipAnalyzer.Core.UI;

using Xunit;

using RimeSpells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Confirms the consolidated registry generator carries physical scalars from
/// <c>data/spelldb.json</c> onto the generated hero spell members.
/// </summary>
public sealed class SpellDatabaseTests
{
    [Fact]
    public void FreezingTorrent_CarriesGeneratedScalars()
    {
        Assert.Equal(15, RimeSpells.FreezingTorrent.Cooldown);
        Assert.Equal(0.4, RimeSpells.FreezingTorrent.ChannelTickInterval);
    }

    [Fact]
    public void FreezingTorrent_CarriesGeneratedAbilityCategory()
    {
        Assert.Equal(AbilityCategory.Core, RimeSpells.FreezingTorrent.AbilityCategory);
    }
}

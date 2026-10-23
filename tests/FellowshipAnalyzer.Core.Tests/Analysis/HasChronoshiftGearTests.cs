using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Item 5984 reads as a different weapon for each hero; <see cref="HasChronoshiftGear"/> activates only for a
/// hero whose 5984 grants Chronoshift.
/// </summary>
public sealed class HasChronoshiftGearTests
{
    private static readonly ReportDungeon TestDungeon =
        new(0, "", 0, null, 0, 0, null, null, null);

    [Theory]
    [InlineData(HeroName.Ardeos)]
    [InlineData(HeroName.Rime)]
    [InlineData(HeroName.Elarion)]
    public void IsActive_WithChronoshiftWeaponEquipped_ReturnsTrue(HeroName hero)
    {
        var context = ContextWithGear(hero, new Item { Id = 5984, Name = "Chronoshift Weapon" });

        Assert.True(HasChronoshiftGear.IsActive(context));
    }

    [Theory]
    [InlineData(HeroName.Tariq)]
    [InlineData(HeroName.Gunde)]
    [InlineData(HeroName.Mara)]
    [InlineData(HeroName.Aeona)]
    [InlineData(HeroName.Sylvie)]
    [InlineData(HeroName.Vigour)]
    public void IsActive_Item5984GrantingAnotherWeaponPower_ReturnsFalse(HeroName hero)
    {
        var context = ContextWithGear(hero, new Item { Id = 5984, Name = "Other Weapon" });

        Assert.False(HasChronoshiftGear.IsActive(context));
    }

    [Fact]
    public void IsActive_WithoutChronoshiftWeapon_ReturnsFalse()
    {
        var context = ContextWithGear(HeroName.Ardeos, new Item { Id = 9999, Name = "Ordinary Weapon" });

        Assert.False(HasChronoshiftGear.IsActive(context));
    }

    private static ParseContext ContextWithGear(HeroName hero, params Item[] gear)
    {
        var combatant = new FullCombatant(new CombatantInfoEvent { Gear = [.. gear] });
        return new ParseContext(PlayerId: 7, Dungeon: TestDungeon, ActorNames: [], combatant, Hero: hero);
    }
}

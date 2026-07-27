using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// The Chronoshift ability is granted by several hero-specific weapons; <see cref="HasChronoshiftGear"/>
/// must activate for every one of them, not a single id.
/// </summary>
public sealed class HasChronoshiftGearTests
{
    private static readonly ReportFight TestFight =
        new(0, "", 0, null, 0, 0, null, null, null);

    [Theory]
    [InlineData(5333)]
    [InlineData(5326)]
    [InlineData(5325)]
    public void IsActive_WithChronoshiftWeaponEquipped_ReturnsTrue(int itemId)
    {
        var context = ContextWithGear(new Item { Id = itemId, Name = "Chronoshift Weapon" });

        Assert.True(HasChronoshiftGear.IsActive(context));
    }

    [Fact]
    public void IsActive_WithoutChronoshiftWeapon_ReturnsFalse()
    {
        var context = ContextWithGear(new Item { Id = 9999, Name = "Ordinary Weapon" });

        Assert.False(HasChronoshiftGear.IsActive(context));
    }

    private static ParseContext ContextWithGear(params Item[] gear)
    {
        var combatant = new Combatant(new CombatantInfoEvent { Gear = [.. gear] });
        return new ParseContext(PlayerId: 7, Fight: TestFight, ActorNames: new Dictionary<int, string>(), combatant);
    }
}

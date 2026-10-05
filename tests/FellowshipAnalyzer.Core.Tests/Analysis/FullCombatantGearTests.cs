using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class FullCombatantGearTests
{
    [Fact]
    public void Constructor_WithSeveralEmptySlots_TreatsThemAsUnequipped()
    {
        var combatant = new FullCombatant(new CombatantInfoEvent
        {
            Gear =
            [
                new Item { Id = 0 },
                new Item { Id = 5234, Name = "Bloodshard Talisman" },
                new Item { Id = 0 },
            ],
        });

        Assert.Null(combatant.Head);
        Assert.Null(combatant.Shoulders);
        Assert.Equal(5234, combatant.Necklace?.Id);
        Assert.False(combatant.HasItem(0));
        Assert.True(combatant.HasItem(5234));
    }

    [Fact]
    public void Constructor_WithTheSameItemInTwoSlots_IndexesItOnce()
    {
        var gear = new List<Item>(Enumerable.Repeat(new Item { Id = 0 }, 9))
        {
            new() { Id = 7001, Name = "Ring" },
            new() { Id = 7001, Name = "Ring" },
        };

        var combatant = new FullCombatant(new CombatantInfoEvent { Gear = gear });

        Assert.Equal(7001, combatant.Ring1?.Id);
        Assert.Equal(7001, combatant.Ring2?.Id);
        Assert.Equal(7001, combatant.GetItem(7001)?.Id);
    }
}

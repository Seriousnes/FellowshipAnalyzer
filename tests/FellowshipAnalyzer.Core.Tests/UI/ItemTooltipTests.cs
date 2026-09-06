using Fellowship.SDK;
using Fellowship.SDK.Client;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Components;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

public class ItemTooltipTests
{
    /// <summary>Aging Skullcap as a combatantinfo event states it.</summary>
    private static Item Worn() => new()
    {
        Id = 3275,
        Name = "Aging Skullcap",
        Quality = 5,
        Icon = "Icon_Mara_Shadowblade_Head_R1_T0.jpg",
        ItemLevel = 330,
        Upgrades = 6,
        MaxUpgrades = 6,
        HasGemSocket = false,
        Gem = new ItemGem { Id = 24, Name = "Splendid Amethyst", Quality = 11 },
        Attributes =
        [
            new ItemAttribute { Id = 1, Name = "Stamina", Value = 400 },
            new ItemAttribute { Id = 4, Name = "Agility", Value = 134 },
            new ItemAttribute { Id = 14, Name = "Haste", Value = 100 },
            new ItemAttribute { Id = 15, Name = "Expertise", Value = 234 },
            new ItemAttribute { Id = 26, Name = "Armor", Value = 968 },
        ],
    };

    private static string Body(Item item, HeroName? hero = HeroName.Mara) =>
        TooltipBody.Write(ItemTooltip.For(item, hero));

    [Fact]
    public void For_StatesTheRungLevelTemperingAndSocket()
    {
        var asked = ItemTooltip.For(Worn(), HeroName.Mara);

        asked.Hero.ShouldBe("Mara");
        asked.Rarity.ShouldBe("5");
        asked.ItemLevel.ShouldBe(330);
        asked.Upgrades.ShouldBe(6);
        asked.MaxUpgrades.ShouldBe(6);
        asked.NoSocket.ShouldBe(true);
        asked.Gem.ShouldBe(24);
    }

    [Fact]
    public void For_TakesArmorOffTheStatLinesOntoItsOwnParameter()
    {
        var asked = ItemTooltip.For(Worn(), HeroName.Mara);

        asked.Armor.ShouldBe(968m);
        asked.Stats.ShouldNotBeNull().ShouldNotContainKey("Armor");
    }

    [Fact]
    public void For_StatesEachStatAtTheMagnitudeTheLogRecords()
    {
        var stats = ItemTooltip.For(Worn(), HeroName.Mara).Stats.ShouldNotBeNull();

        stats["Stamina"].Rating.ShouldBe([400m]);
        stats["Agility"].Rating.ShouldBe([134m]);
        stats["Haste"].Rating.ShouldBe([100m]);
        stats["Expertise"].Rating.ShouldBe([234m]);
    }

    [Fact]
    public void For_StatesAStatOncePerSlotItFills()
    {
        var item = Worn();
        item.Attributes.Add(new ItemAttribute { Id = 4, Name = "Agility", Value = 66 });

        ItemTooltip.For(item, HeroName.Mara).Stats.ShouldNotBeNull()["Agility"].Rating
            .ShouldBe([134m, 66m]);
    }

    [Fact]
    public void For_NamesNoRandomRollsSoNoStatCanFallOutsideThePool()
    {
        ItemTooltip.For(Worn(), HeroName.Mara).RandomStats.ShouldBeNull();
        Body(Worn()).ShouldNotContain("randomStats");
    }

    [Fact]
    public void For_ReadsATraitBackToItsNativeId()
    {
        var item = Worn();
        item.Traits.Add(new ItemTrait { Id = FSLID.FromNative(SpellKind.Weapon, 456).Value, Rank = 2 });

        ItemTooltip.For(item, HeroName.Mara).Modifiers.ShouldBe(
            [new ItemModifier(ModifierKind.Trait, 456, 2)]);

        Body(item).ShouldContain("""{"kind":"Trait","id":456,"amount":2}""");
    }

    [Fact]
    public void Write_StatesTheItemAsTheCodexReadsItBack() =>
        Body(Worn()).ShouldBe(
            """
            {"hero":"Mara","rarity":"5","itemLevel":330,"upgrades":6,"maxUpgrades":6,"noSocket":true,
            "gem":24,"armor":968,"stats":{"Stamina":{"rating":[400]},"Agility":{"rating":[134]},
            "Haste":{"rating":[100]},"Expertise":{"rating":[234]}}}
            """.ReplaceLineEndings(string.Empty));

    [Fact]
    public void For_LeavesModifiersOffAnItemCarryingNoTrait() =>
        ItemTooltip.For(Worn(), HeroName.Mara).Modifiers.ShouldBeNull();

    [Fact]
    public void For_AddressesTheTooltipByTypeAndIdAlone() =>
        CodexAddresses.Tooltip(EntityType.Item, 3275).ShouldBe("api/item/3275/tooltip");

    [Fact]
    public void For_StatesOnlyTheHeroForAnItemTheReportKnowsNothingMoreAbout()
    {
        var asked = ItemTooltip.For(HeroName.Mara);

        asked.Hero.ShouldBe("Mara");
        asked.Stats.ShouldBeNull();
        TooltipBody.Write(asked).ShouldBe("""{"hero":"Mara"}""");
    }
}

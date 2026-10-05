using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

using GearItem = FellowshipAnalyzer.Core.Events.Item;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Covers <see cref="PassiveBonusTracker"/>: the stat effects that no apply or remove event marks, resolved
/// from the player's gem power, set pieces, and the dungeon's modifiers, and re-resolved while an Anomalous
/// Residue raises a gem's power.
/// </summary>
public sealed class PassiveBonusTrackerTests
{
    private const int PlayerId = 7;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task GemPower_AddsTheFlatPercentageEachUnlockedTraitGrants()
    {
        var tracker = await Run(new CombatantInfoEvent { Amethyst = 1250, Topaz = 300, Emerald = 300, Sapphire = 1500 });

        Assert.Equal(StatTracker.BaseCritChance + 0.06, tracker.CurrentCritPercentage, precision: 10);
        Assert.Equal(0.02, tracker.CurrentHastePercentage, precision: 10);
        Assert.Equal(0.02, tracker.CurrentExpertisePercentage, precision: 10);
        Assert.Equal(0.06, tracker.CurrentSpiritPercentage, precision: 10);
    }

    [Fact]
    public async Task GemPower_LeavesItsRatingTraitsToTheCombatantinfoTotals()
    {
        var tracker = await Run(new CombatantInfoEvent { Topaz = 800, Haste = 100 });

        Assert.Equal(100, tracker.CurrentHasteRating, precision: 6);
    }

    [Fact]
    public async Task AnomalousResidue_UnlocksTheTraitsItsGemPowerReaches()
    {
        var tracker = await Run(
            new CombatantInfoEvent { Haste = 100 },
            [ApplyBuff(1000, Items.AnomalousResidueTopaz)]);

        Assert.Equal(108, tracker.CurrentHasteRating, precision: 6);
        Assert.Equal(StatTracker.RatingToPercentage(108) + 0.02, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task AnomalousResidue_AddsGemPowerForEveryStack()
    {
        var tracker = await Run(
            new CombatantInfoEvent { Haste = 100 },
            [
                ApplyBuff(1000, Items.AnomalousResidueTopaz),
                ApplyBuffStack(2000, Items.AnomalousResidueTopaz, stack: 2),
            ]);

        Assert.Equal(124, tracker.CurrentHasteRating, precision: 6);
        Assert.Equal(StatTracker.RatingToPercentage(124) + 0.02, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task AnomalousResidue_WithdrawsWhatItUnlockedWhenItEnds()
    {
        var tracker = await Run(
            new CombatantInfoEvent { Haste = 100 },
            [
                ApplyBuff(1000, Items.AnomalousResidueTopaz),
                RemoveBuff(2000, Items.AnomalousResidueTopaz),
            ]);

        Assert.Equal(100, tracker.CurrentHasteRating, precision: 6);
        Assert.Equal(StatTracker.RatingToPercentage(100), tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task AnomalousResidue_RaisesBlessingOfTheCommanderToTheRankItReaches()
    {
        var tracker = await Run(
            new CombatantInfoEvent { Emerald = 1100 },
            [ApplyBuff(1000, Items.AnomalousResidueEmerald)]);

        Assert.Equal(0.12, tracker.CurrentAbilityCooldownReduction(null), precision: 10);
        Assert.Equal(0.04, tracker.StartingAbilityCooldownReduction(null), precision: 10);
    }

    [Fact]
    public async Task CompletedSet_AddsItsBonus()
    {
        var tracker = await Run(new CombatantInfoEvent
        {
            Gear =
            [
                new GearItem { Id = 1, Set = new ItemSet { Id = 689, Name = "Sinthara's Veil" } },
                new GearItem { Id = 2, Set = new ItemSet { Id = 689, Name = "Sinthara's Veil" } },
            ],
        });

        Assert.Equal(0.04, tracker.CurrentSpiritPercentage, precision: 10);
    }

    [Fact]
    public async Task IncompleteSet_AddsNothing()
    {
        var tracker = await Run(new CombatantInfoEvent
        {
            Gear = [new GearItem { Id = 1, Set = new ItemSet { Id = 680, Name = "Tuzari Grace" } }],
        });

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task UltimatumModifier_ReducesEveryAbilityCooldown()
    {
        var tracker = await Run(
            new CombatantInfoEvent(),
            dungeon: TestDungeon with { Modifiers = [PassiveBonusTracker.UltimatumModifier] });

        Assert.Equal(0.10, tracker.CurrentAbilityCooldownReduction(null), precision: 10);
    }

    [Fact]
    public async Task ChallengeModifier_RaisesSpirit()
    {
        var tracker = await Run(
            new CombatantInfoEvent(),
            dungeon: TestDungeon with { Modifiers = [PassiveBonusTracker.ChallengeModifier] });

        Assert.Equal(0.10, tracker.CurrentSpiritPercentage, precision: 10);
    }

    [Fact]
    public async Task SetPassiveBonus_ReplacesTheSourcesPreviousContribution()
    {
        var tracker = await Run(new CombatantInfoEvent());
        var source = new object();

        tracker.SetPassiveBonus(source, new PassiveStatBonus { Haste = 0.05 });
        tracker.SetPassiveBonus(source, new PassiveStatBonus { Haste = 0.02, AbilityCooldownReduction = 0.10 });

        Assert.Equal(0.02, tracker.CurrentHastePercentage, precision: 10);
        Assert.Equal(0.10, tracker.CurrentAbilityCooldownReduction(null), precision: 10);

        tracker.SetPassiveBonus(source, PassiveStatBonus.None);

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
        Assert.Equal(0.0, tracker.CurrentAbilityCooldownReduction(null), precision: 10);
    }

    private static async Task<StatTracker> Run(
        CombatantInfoEvent info,
        List<Event>? events = null,
        ReportDungeon? dungeon = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        info.Timestamp = 0;
        info.SourceId = PlayerId;

        List<Event> stream =
        [
            info,
            new DungeonStartEvent { Timestamp = 0 },
            .. events ?? [],
        ];

        var parser = new TestParser(emitter, provider, [typeof(StatTracker), typeof(PassiveBonusTracker)]);
        await parser.Analyze(stream, PlayerId, dungeon: dungeon ?? TestDungeon);

        return parser.GetModule<StatTracker>()!;
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;
    }

    private static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, Spell spell, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        Stack = stack,
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };
}

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Covers the shared <see cref="StatBuffs"/> table: that a hero needs no registration to have the effects
/// the game data describes tracked, and that the magnitudes which depend on the player's gear or state
/// resolve from the combatantinfo rather than a constant.
/// </summary>
public sealed partial class StatBuffsTests
{
    private const int PlayerId = 7;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Theory]
    [InlineData(1001350)]
    [InlineData(1002253)]
    [InlineData(1002277)]
    [InlineData(1002662)]
    public void EverySpiritOfHeroismVariant_GrantsThirtyPercentHaste(int fslid)
    {
        var buff = StatBuffs.Percentages[fslid];

        Assert.Equal(0.30, buff.Haste!.AsT0, precision: 10);
    }

    [Fact]
    public async Task SpiritOfHeroism_RaisesHasteWithoutAnyHeroRegistration()
    {
        var tracker = await Run([ApplyDebuff(1000, Spells.SpiritOfHeroismNotTank.FSLID)]);

        Assert.Equal(0.30, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task MightOfTheMinotaur_ScalesMainStatByItsMultiplier()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.MightOfTheMinotaurII.FSLID)],
            mainStat: 300);

        Assert.Equal(300 * 1.06, tracker.CurrentMainStat, precision: 6);
    }

    [Fact]
    public async Task TheWayfarer_TakesItsHasteFromTheBlessingLevelTheGearReports()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.TheWayfarer.FSLID)],
            blessings: [new ItemBlessing { Id = 4000043, Level = 2, Name = "The Wayfarer" }]);

        Assert.Equal(0.10, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task TheWayfarer_ContributesNothingWhenThePlayerHasNoSuchBlessing()
    {
        var tracker = await Run([ApplyBuff(1000, Items.TheWayfarer.FSLID)]);

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task TheTrickster_TakesItsCritFromTheBlessingLevel()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.TheTrickster.FSLID)],
            blessings: [new ItemBlessing { Id = 4000033, Level = 4, Name = "The Trickster" }]);

        Assert.Equal(StatTracker.BaseCritChance + 0.10, tracker.CurrentCritPercentage, precision: 10);
    }

    [Fact]
    public async Task ThePhilosopher_SizesAllThreeStatsFromTheSpiritStatAtApplication()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.ThePhilosopher.FSLID)],
            spirit: 50,
            blessings: [new ItemBlessing { Id = 4000009, Level = 2, Name = "The Philosopher" }]);

        Assert.Equal(0.0064, tracker.AdditionalHaste, precision: 10);
        Assert.Equal(0.0064, tracker.AdditionalExpertise, precision: 10);
        Assert.Equal(0.0064, tracker.AdditionalCrit, precision: 10);
    }

    [Fact]
    public async Task ThePhilosopher_TakesItsTierFromEverySlottedCopy()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.ThePhilosopher.FSLID)],
            spirit: 50,
            blessings:
            [
                new ItemBlessing { Id = 4000174, Level = 2, Name = "The Philosopher" },
                new ItemBlessing { Id = 4000174, Level = 2, Name = "The Philosopher" },
            ]);

        Assert.Equal(0.016, tracker.AdditionalHaste, precision: 10);
    }

    [Fact]
    public async Task ThePhilosopher_IgnoresHowFullTheSpiritBarIs()
    {
        var apply = ApplyBuff(1000, Items.ThePhilosopher.FSLID);
        apply.TargetResources = new ActorResources
        {
            Resources = [new ClassResource { Type = ResourceTypes.Spirit, Amount = 100, Max = 100 }],
        };

        var tracker = await Run(
            [apply],
            blessings: [new ItemBlessing { Id = 4000009, Level = 4, Name = "The Philosopher" }]);

        Assert.Equal(0.0, tracker.AdditionalHaste, precision: 10);
    }

    [Fact]
    public async Task ThePhilosopher_CountsSpiritAboveHalfAsHalf()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.ThePhilosopher.FSLID)],
            spirit: 400,
            blessings: [new ItemBlessing { Id = 4000009, Level = 4, Name = "The Philosopher" }]);

        Assert.Equal(0.10, tracker.AdditionalHaste, precision: 10);
    }

    [Fact]
    public async Task FocusedHaste_TakesItsHasteRatingFromTheTraitRankAndTheStackCount()
    {
        var tracker = await Run(
            [
                ApplyBuff(1000, Items.FocusedHasteBuff.FSLID),
                ApplyBuffStack(2000, Items.FocusedHasteBuff.FSLID, stack: 4),
            ],
            traits: [new ItemTrait { Id = Items.HuntersFocusTrait.FSLID, Rank = 3, Name = "Hunter's Focus" }]);

        Assert.Equal(12 * 4, tracker.CurrentHasteRating, precision: 6);
    }

    [Fact]
    public async Task SeizedOpportunity_TakesItsCritRatingFromTheTraitRank()
    {
        var tracker = await Run(
            [ApplyBuff(1000, Items.SeizedOpportunityBuff.FSLID)],
            traits: [new ItemTrait { Id = Items.SeizedOpportunityTrait.FSLID, Rank = 4, Name = "Seized Opportunity" }]);

        Assert.Equal(75, tracker.CurrentCritRating, precision: 6);
    }

    [Fact]
    public async Task HarmoniousSoul_AddsAllFourSecondariesPerStack()
    {
        var tracker = await Run(
            [
                ApplyBuff(1000, Items.HarmoniousSoulII.FSLID),
                ApplyBuffStack(2000, Items.HarmoniousSoulII.FSLID, stack: 5),
            ]);

        Assert.Equal(0.03, tracker.AdditionalCrit, precision: 10);
        Assert.Equal(0.03, tracker.AdditionalHaste, precision: 10);
        Assert.Equal(0.03, tracker.AdditionalExpertise, precision: 10);
        Assert.Equal(0.03, tracker.AdditionalSpirit, precision: 10);
    }

    [Fact]
    public async Task AdrenalineRush_AddsItsHasteAndTakesItBackOnRemoval()
    {
        var tracker = await Run(
            [
                ApplyBuff(1000, Items.AdrenalineRushII.FSLID),
                RemoveBuff(2000, Items.AdrenalineRushII.FSLID),
            ]);

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task AdrenalineRush_AddsItsHasteWhileActive()
    {
        var tracker = await Run([ApplyBuff(1000, Items.AdrenalineRushII.FSLID)]);

        Assert.Equal(0.09, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task EssenceOfTheVirtuoso_AddsTheHasteItRetains()
    {
        var tracker = await Run([ApplyBuff(1000, Items.EssenceOfTheVirtuoso.FSLID)]);

        Assert.Equal(0.02, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task RuneRush_MultipliesOverallSpeedRatherThanAddingHaste()
    {
        var tracker = await Run(
            [
                ApplyDebuff(1000, Spells.SpiritOfHeroismNotTank.FSLID),
                ApplyBuff(2000, Spells.RuneRush.FSLID),
            ]);

        Assert.Equal(1.30 * 1.15 - 1.0, tracker.CurrentHastePercentage, precision: 10);
        Assert.Equal(1.15, tracker.HasteMultiplier, precision: 10);
    }

    [Fact]
    public async Task RuneRush_LeavesOverallSpeedAsItWasOnceItEnds()
    {
        var tracker = await Run(
            [
                ApplyDebuff(1000, Spells.SpiritOfHeroismNotTank.FSLID),
                ApplyBuff(2000, Spells.RuneRush.FSLID),
                RemoveBuff(3000, Spells.RuneRush.FSLID),
            ]);

        Assert.Equal(0.30, tracker.CurrentHastePercentage, precision: 10);
        Assert.Equal(1.0, tracker.HasteMultiplier, precision: 10);
    }

    [Fact]
    public async Task ShadowsDefeat_MultipliesOverallSpeed()
    {
        var tracker = await Run([ApplyBuff(1000, Spells.ShadowsDefeat.FSLID)]);

        Assert.Equal(0.20, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task StormsFury_AddsCriticalStrikeChance()
    {
        var tracker = await Run([ApplyBuff(1000, Spells.StormsFury.FSLID)]);

        Assert.Equal(StatTracker.BaseCritChance + 0.20, tracker.CurrentCritPercentage, precision: 10);
    }

    [Fact]
    public async Task WrathOfWinter_GrantsNoHaste()
    {
        var tracker = await Run([ApplyBuff(1000, 1001387)]);

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    private static async Task<StatTracker> Run(
        List<Event> events,
        int mainStat = 0,
        int spirit = 0,
        List<ItemBlessing>? blessings = null,
        List<ItemTrait>? traits = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        List<Event> stream =
        [
            new CombatantInfoEvent
            {
                Timestamp = 0,
                SourceId = PlayerId,
                Intellect = mainStat,
                Spirit = spirit,
                Gear = [new Events.Item { Id = 1, Blessings = blessings ?? [], Traits = traits ?? [] }],
            },
            new DungeonStartEvent { Timestamp = 0 },
            .. events,
        ];

        var parser = new TestParser(emitter, provider, [typeof(StatTracker)]);
        await parser.Analyze(stream, PlayerId, dungeon: TestDungeon);

        return parser.GetModule<StatTracker>()!;
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;
    }

    private static ApplyBuffEvent ApplyBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };
}

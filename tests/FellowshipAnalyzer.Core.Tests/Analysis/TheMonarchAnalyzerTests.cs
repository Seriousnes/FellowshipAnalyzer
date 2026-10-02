using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

using GearItem = FellowshipAnalyzer.Core.Events.Item;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Covers <see cref="TheMonarchAnalyzer"/>: Cooldown Acceleration for MAJOR abilities sized by the
/// blessing's tier plus a share of the player's haste, capped, and replaced as haste moves.
/// </summary>
public sealed class TheMonarchAnalyzerTests
{
    private const int PlayerId = 7;
    private const int MajorSpell = 101;
    private const int OtherSpell = 102;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static readonly List<ItemBlessing> TierTwo =
    [
        new() { Id = 4000177, Level = 1, Name = TheMonarchAnalyzer.Blessing },
        new() { Id = 4000177, Level = 1, Name = TheMonarchAnalyzer.Blessing },
    ];

    [Fact]
    public async Task MajorAbilities_GainTheTierAcceleration()
    {
        var (tracker, abilities) = await Run(TierTwo);

        Assert.Equal(0.05, tracker.CurrentCooldownAcceleration(abilities.GetAbility(MajorSpell)), precision: 10);
        Assert.Equal(0.0, tracker.CurrentCooldownAcceleration(abilities.GetAbility(OtherSpell)), precision: 10);
    }

    [Fact]
    public async Task Haste_AddsATenthOfItselfToTheAcceleration()
    {
        var (tracker, abilities) = await Run(TierTwo, [ApplyDebuff(1000, Spells.SpiritOfHeroismNotTank)]);

        Assert.Equal(0.05 + 0.03, tracker.CurrentCooldownAcceleration(abilities.GetAbility(MajorSpell)), precision: 10);
    }

    [Fact]
    public async Task HastePart_StopsAtFivePercent()
    {
        var (tracker, abilities) = await Run(
            TierTwo,
            [
                ApplyDebuff(1000, Spells.SpiritOfHeroismNotTank),
                ApplyBuff(2000, Items.DarkProphecy),
            ]);

        Assert.Equal(0.05 + 0.05, tracker.CurrentCooldownAcceleration(abilities.GetAbility(MajorSpell)), precision: 10);
    }

    [Fact]
    public async Task WithoutTheBlessing_NothingIsAccelerated()
    {
        var (tracker, abilities) = await Run([], [ApplyDebuff(1000, Spells.SpiritOfHeroismNotTank)]);

        Assert.Equal(0.0, tracker.CurrentCooldownAcceleration(abilities.GetAbility(MajorSpell)), precision: 10);
    }

    [Fact]
    public void Predicate_EnablesTheAnalyzerOnlyForAPlayerWithTheBlessing()
    {
        static ParseContext ContextFor(List<ItemBlessing> blessings) => new(
            PlayerId,
            TestDungeon,
            [],
            new FullCombatant(new CombatantInfoEvent { Gear = [new GearItem { Id = 1, Blessings = blessings }] }));

        Assert.True(HasTheMonarch.IsActive(ContextFor(TierTwo)));
        Assert.False(HasTheMonarch.IsActive(ContextFor([])));
    }

    private static async Task<(StatTracker Tracker, Abilities Abilities)> Run(
        List<ItemBlessing> blessings,
        List<Event>? events = null)
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
                Gear = [new GearItem { Id = 1, Blessings = blessings }],
            },
            new DungeonStartEvent { Timestamp = 0 },
            .. events ?? [],
        ];

        Type[] moduleTypes =
        [
            typeof(TestAbilities),
            typeof(StatTracker),
            typeof(Haste),
            typeof(TheMonarchAnalyzer),
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(stream, PlayerId, dungeon: TestDungeon);

        return (parser.GetModule<StatTracker>()!, parser.GetModule<Abilities>()!);
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type) =>
            type == typeof(TestAbilities) ? new TestAbilities() : base.CreateInstance(type);
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = MajorSpell, Name = "Major", Cooldown = 30.0, AbilityCategory = AbilityCategory.Major },
                Category = SpellCategory.Utility,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = OtherSpell, Name = "Other", Cooldown = 30.0 },
                Category = SpellCategory.Rotational,
            },
        ];
    }

    private static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };
}

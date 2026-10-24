using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="Abilities"/> applying a <see cref="ChargeGrant"/>: the granted charges and Haste
/// placement for a player holding the talent or legendary, the spell's own values otherwise, and a
/// spellbook's <see cref="SpellbookAbility.CooldownReducedByHaste"/> overriding the placement.
/// </summary>
public sealed class ChargeGrantTests
{
    private const int PlayerId = 7;
    private const int TalentGranted = 201;
    private const int LegendaryGranted = 202;
    private const int OverriddenGranted = 203;
    private const int GrantingTalent = 566;
    private const int GrantingLegendary = 5898;
    private const int LegendaryQuality = 6;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task ATalentGrant_GivesItsChargesAndTimeRateHaste()
    {
        var abilities = await Run(talents: [GrantingTalent]);

        Assert.Equal(2, abilities.GetMaxCharges(TalentGranted));
        Assert.Equal(CooldownHasteScaling.TimeRate, abilities.GetAbility(TalentGranted)!.CooldownHaste);
    }

    [Fact]
    public async Task AnUnheldGrant_LeavesTheSpellsOwnValues()
    {
        var abilities = await Run();

        Assert.Equal(1, abilities.GetMaxCharges(TalentGranted));
        Assert.Equal(CooldownHasteScaling.Duration, abilities.GetAbility(TalentGranted)!.CooldownHaste);
        Assert.Equal(1, abilities.GetMaxCharges(LegendaryGranted));
    }

    [Fact]
    public async Task ALegendaryGrant_AppliesWhileTheLegendaryIsEquipped()
    {
        var abilities = await Run(legendary: GrantingLegendary);

        Assert.Equal(3, abilities.GetMaxCharges(LegendaryGranted));
        Assert.Equal(CooldownHasteScaling.TimeRate, abilities.GetAbility(LegendaryGranted)!.CooldownHaste);
    }

    [Fact]
    public async Task ASpellbookHasteFlag_OverridesTheGrantsPlacement()
    {
        var abilities = await Run(talents: [GrantingTalent]);

        Assert.Equal(2, abilities.GetMaxCharges(OverriddenGranted));
        Assert.Null(abilities.GetAbility(OverriddenGranted)!.CooldownHaste);
    }

    private static async Task<Abilities> Run(int[]? talents = null, int? legendary = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        List<Event> events =
        [
            new CombatantInfoEvent
            {
                Timestamp = 0,
                SourceId = PlayerId,
                Talents = [.. (talents ?? []).Select(id => new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, id).Value })],
                Gear = legendary is { } itemId ? [new Item { Id = itemId, Quality = LegendaryQuality }] : [],
            },
        ];

        var parser = new TestParser(emitter, provider);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);

        return parser.GetModule<Abilities>()!;
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider) : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [typeof(TestAbilities), typeof(DebugAnnotations)];

        protected override Type[] GetNormalizerTypes() => [typeof(DungeonBookendNormalizer)];

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(DungeonBookendNormalizer)) return new DungeonBookendNormalizer(CurrentParseContext);
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        private static readonly ChargeGrant TalentGrant = new()
        {
            Source = GrantSource.Talent,
            Id = GrantingTalent,
            Charges = 2,
            CooldownHaste = CooldownHasteScaling.TimeRate,
        };

        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell
                {
                    Id = TalentGranted, Name = "Talent Granted", Cooldown = 20,
                    CooldownHaste = CooldownHasteScaling.Duration, ChargeGrant = TalentGrant,
                },
                Category = SpellCategory.Rotational,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell
                {
                    Id = LegendaryGranted, Name = "Legendary Granted", Cooldown = 20,
                    ChargeGrant = TalentGrant with { Source = GrantSource.LegendaryItem, Id = GrantingLegendary, Charges = 3 },
                },
                Category = SpellCategory.Rotational,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = OverriddenGranted, Name = "Overridden Granted", Cooldown = 20, ChargeGrant = TalentGrant },
                Category = SpellCategory.Rotational,
                CooldownReducedByHaste = false,
            },
        ];
    }
}

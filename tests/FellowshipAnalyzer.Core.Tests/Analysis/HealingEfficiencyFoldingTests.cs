using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// A periodic heal that arrives under its own effect id has to land on the ability that cast it.
/// The cast carries the cost and the ticks carry the healing, so leaving them in separate rows makes
/// both the healing-per-resource and healing-per-execution-time rates read zero on the one ability
/// where they matter most.
/// </summary>
public sealed class HealingEfficiencyFoldingTests
{
    private const int PlayerId = 7;
    private const int TargetId = 8;
    private const int CastSpell = 1060;
    private const int TickEffect = 1_001_445;
    private const int UnknownSpell = 4242;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task TicksUnderAnEffectIdLandOnTheAbilityThatPaidForThem()
    {
        var tracker = await Analyze(
        [
            Cast(1_000, CastSpell),
            Heal(2_000, TickEffect, 400, 100),
            Heal(4_000, TickEffect, 600, 200),
        ]);

        tracker.For(TickEffect).ShouldBeNull();

        var spell = tracker.For(CastSpell).ShouldNotBeNull();
        spell.Casts.ShouldBe(1);
        spell.Heals.ShouldBe(2);
        spell.Effective.ShouldBe(1_000);
        spell.Overheal.ShouldBe(300);
        spell.ResourceSpent.ShouldBe(TestFoldingTracker.CostPerCast);
        spell.HealingPerResource.ShouldBe(1_000 / (double)TestFoldingTracker.CostPerCast, 0.001);
    }

    [Fact]
    public async Task ASpellTheSpellbookDoesNotKnowKeepsItsOwnRow()
    {
        var tracker = await Analyze([Heal(2_000, UnknownSpell, 500, 0)]);

        tracker.For(UnknownSpell).ShouldNotBeNull().Effective.ShouldBe(500);
    }

    private static async Task<TestFoldingTracker> Analyze(List<Event> events)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        Type[] moduleTypes = [typeof(FoldingAbilities), typeof(DebugAnnotations), typeof(Combatants), typeof(TestFoldingTracker)];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);
        return parser.GetModule<TestFoldingTracker>()!;
    }

    private static HealEvent Heal(int timestamp, int spellId, long amount, long overheal) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TargetId,
        Ability = new Ability { FSLID = spellId, Name = "Heal" },
        Amount = amount,
        Overheal = overheal,
    };

    private static CastEvent Cast(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TargetId,
        Ability = new Ability { FSLID = spellId, Name = "Cast" },
    };

    private sealed class TestFoldingTracker : HealingEfficiencyTracker
    {
        public const int CostPerCast = 39;

        protected override int ResourceCostOf(CastEvent castEvent) =>
            castEvent.Ability.Id == CastSpell ? CostPerCast : 0;
    }

    private sealed class FoldingAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new()
            {
                PrimarySpell = new Spell { Id = CastSpell, Name = "Fluttercall: Heal" },
                AdditionalSpells = [new Spell { Id = TickEffect, Name = "Fluttercall: Heal" }],
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
            },
        ];
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(FoldingAbilities)) return new FoldingAbilities();
            if (type == typeof(TestFoldingTracker)) return new TestFoldingTracker();
            return base.CreateInstance(type);
        }
    }
}

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Statistics;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

/// <summary>
/// Tests for <see cref="CastEfficiencyModel"/>: the denominator is the summed pull duration rather
/// than the dungeon span, recharges are clipped to those pulls, and the cast ceiling is derived from the
/// recharge period the report shows rather than the curated cooldown.
/// </summary>
public sealed class CastEfficiencyModelTests
{
    private const int PlayerId = 7;
    private const int SpellId = 100;
    private const int ExtraSpellId = 101;
    private const int FillerId = 200;

    [Fact]
    public void DowntimeBetweenPulls_IsExcludedFromTheDenominator()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000), Pull(1, 60_000, 70_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 1000),
            Begin(SpellId, 1000),
            End(SpellId, 6000),
        };

        var row = Single(Build(events, pulls));

        row.Efficiency.ShouldNotBeNull();
        row.Efficiency.Value.ShouldBe(5000 / 20_000d, 0.0001);
        row.CastsPerMinute.ShouldBe(1 / (20_000 / 60_000d), 0.0001);
    }

    [Fact]
    public void RechargeStraddlingAPullEnd_CountsOnlyTheTimeInsideThePull()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000), Pull(1, 60_000, 70_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 8000),
            Begin(SpellId, 8000),
            End(SpellId, 28_000),
        };

        var row = Single(Build(events, pulls));

        row.Efficiency!.Value.ShouldBe(2000 / 20_000d, 0.0001);
    }

    [Fact]
    public void CooldownStillRunningAtTheEnd_CountsUntilTheLastPullEnd()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 4000),
            Begin(SpellId, 4000),
        };

        var row = Single(Build(events, pulls));

        row.Efficiency!.Value.ShouldBe(6000 / 10_000d, 0.0001);
    }

    [Fact]
    public void MaxCasts_UsesTheObservedRechargeRatherThanTheCuratedCooldown()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 40_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 0),
            Begin(SpellId, 0),
            End(SpellId, 5000),
            Cast(SpellId, 5000),
            Begin(SpellId, 5000),
            End(SpellId, 10_000),
        };

        var row = Single(Build(events, pulls));

        row.Casts.ShouldBe(2);
        row.MaxCasts.ShouldBe(9);
    }

    [Fact]
    public void MaxCasts_FallsBackToTheCuratedCooldownWhenNoRechargeCompletedInsideAPull()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 40_000) };
        var events = new List<Event>();

        var row = Single(Build(events, pulls));

        row.Casts.ShouldBe(0);
        row.MaxCasts.ShouldBe(3);
        row.Efficiency!.Value.ShouldBe(0d);
    }

    [Fact]
    public void RechargeCompletingOutsideThePull_DoesNotShortenTheObservedPeriod()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 40_000) };
        var straddling = new List<Event>
        {
            Cast(SpellId, 35_000),
            Begin(SpellId, 35_000),
            End(SpellId, 55_000),
        };

        var row = Single(Build(straddling, pulls));

        row.MaxCasts.ShouldBe(3);
    }

    [Fact]
    public void CastsOutsideEveryPull_AreNotCounted()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 5000),
            Cast(SpellId, 30_000),
        };

        var row = Single(Build(events, pulls));

        row.Casts.ShouldBe(1);
    }

    [Fact]
    public void AdditionalSpells_CountTowardsTheirPrimaryAbility()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 1000),
            Cast(ExtraSpellId, 2000),
        };

        var row = Single(Build(events, pulls));

        row.Casts.ShouldBe(2);
    }

    [Fact]
    public void HiddenAbilities_AndUnusedAbilitiesWithoutACooldown_AreOmitted()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000) };

        var rows = Build([], pulls);

        rows.Select(row => (int)row.Ability.PrimarySpell.FSLID).ShouldBe([SpellId]);
    }

    [Fact]
    public void AnAbilityWithoutACooldown_ReportsCastsButNoEfficiency()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 30_000) };
        var events = new List<Event> { Cast(FillerId, 1000), Cast(FillerId, 2000) };

        var filler = Build(events, pulls).Single(row => (int)row.Ability.PrimarySpell.FSLID == FillerId);

        filler.Casts.ShouldBe(2);
        filler.Efficiency.ShouldBeNull();
        filler.MaxCasts.ShouldBeNull();
        filler.CastsPerMinute.ShouldBe(4, 0.0001);
    }

    [Fact]
    public void WithoutACastEfficiencyEntry_NoRecommendationIsReported()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 40_000) };

        Single(Build([], pulls)).MeetsRecommendation.ShouldBeNull();
    }

    [Theory]
    [InlineData(30_000, true)]
    [InlineData(10_000, false)]
    public void WithACastEfficiencyEntry_TheRecommendationIsCompared(int rechargeMs, bool expected)
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 40_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 0),
            Begin(SpellId, 0),
            End(SpellId, rechargeMs),
        };

        var abilities = new RecommendedSpellbook();
        var row = CastEfficiencyModel.Build(abilities, events, PlayerId, pulls).Single();

        row.MeetsRecommendation.ShouldBe(expected);
    }

    [Fact]
    public void ChargeRestores_CountAsSeparateRechargePeriods()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 40_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 0),
            Begin(SpellId, 0),
            Restore(SpellId, 5000, stillOnCooldown: true),
            Restore(SpellId, 11_000, stillOnCooldown: true),
            End(SpellId, 18_000),
        };

        var row = Single(Build(events, pulls));

        row.Efficiency!.Value.ShouldBe(18_000 / 40_000d, 0.0001);
        row.MaxCasts.ShouldBe(7);
    }

    [Fact]
    public void EventsFromAnotherActor_AreIgnored()
    {
        var pulls = new List<PullStartEvent> { Pull(0, 0, 10_000) };
        var events = new List<Event>
        {
            Cast(SpellId, 1000, sourceId: PlayerId + 1),
            Begin(SpellId, 1000, sourceId: PlayerId + 1),
            End(SpellId, 6000, sourceId: PlayerId + 1),
        };

        var row = Single(Build(events, pulls));

        row.Casts.ShouldBe(0);
        row.Efficiency!.Value.ShouldBe(0d);
    }

    [Fact]
    public void PullsWithNoDuration_ProduceNoRows()
    {
        CastEfficiencyModel.Build(new TestSpellbook(), [], PlayerId, []).ShouldBeEmpty();
    }

    private static List<AbilityCastEfficiency> Build(List<Event> events, List<PullStartEvent> pulls) =>
        CastEfficiencyModel.Build(new TestSpellbook(), events, PlayerId, pulls);

    private static AbilityCastEfficiency Single(List<AbilityCastEfficiency> rows) =>
        rows.Single(row => (int)row.Ability.PrimarySpell.FSLID == SpellId);

    private static PullStartEvent Pull(int index, int start, int end) =>
        new()
        {
            Timestamp = start,
            End = new PullEndEvent { Timestamp = end },
            Index = index,
            Id = index + 1,
            Name = $"Pull {index}",
            Targets = PullKind.Multi,
            IsBoss = false,
            Kill = true,
        };

    private static CastEvent Cast(int spellId, int timestamp, int sourceId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        Ability = new Ability { FSLID = spellId, Name = "Spell" },
    };

    private static UpdateSpellUsableEvent Begin(int spellId, int timestamp, int sourceId = PlayerId) =>
        Update(UpdateSpellUsableType.BeginCooldown, spellId, timestamp, onCooldown: true, sourceId);

    private static UpdateSpellUsableEvent End(int spellId, int timestamp, int sourceId = PlayerId) =>
        Update(UpdateSpellUsableType.EndCooldown, spellId, timestamp, onCooldown: false, sourceId);

    private static UpdateSpellUsableEvent Restore(int spellId, int timestamp, bool stillOnCooldown) =>
        Update(UpdateSpellUsableType.RestoreCharge, spellId, timestamp, stillOnCooldown, PlayerId);

    private static UpdateSpellUsableEvent Update(
        UpdateSpellUsableType type, int spellId, int timestamp, bool onCooldown, int sourceId) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        UpdateType = type,
        IsOnCooldown = onCooldown,
        Ability = new Ability { FSLID = spellId, Name = "Spell" },
    };

    private sealed class TestSpellbook : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new()
            {
                PrimarySpell = new Spell { Id = SpellId, Name = "Cooldown Spell", Cooldown = 15 },
                AdditionalSpells = [new Spell { Id = ExtraSpellId, Name = "Cooldown Spell Echo" }],
                Category = SpellCategory.Cooldowns,
            },
            new()
            {
                PrimarySpell = new Spell { Id = FillerId, Name = "Filler" },
                Category = SpellCategory.Rotational,
            },
            new()
            {
                PrimarySpell = new Spell { Id = 300, Name = "Auto Attack", Cooldown = 2 },
                Category = SpellCategory.Hidden,
            },
        ];
    }

    private sealed class RecommendedSpellbook : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new()
            {
                PrimarySpell = new Spell { Id = SpellId, Name = "Cooldown Spell", Cooldown = 15 },
                Category = SpellCategory.Cooldowns,
                CastEfficiency = new CastEfficiencyInfo { Suggestion = true, RecommendedEfficiency = 0.5 },
            },
        ];
    }
}

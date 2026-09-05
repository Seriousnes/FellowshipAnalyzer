using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Guides;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

/// <summary>
/// Tests for <see cref="CooldownGraphModel"/>: the lane covers the render window with no gaps, time
/// outside every pull is set aside rather than read as a hold, and how much of a hold is marked
/// follows the ability's <see cref="CooldownUsage"/>.
/// </summary>
public sealed class CooldownGraphModelTests
{
    private const int PlayerId = 7;
    private const int SpellId = 100;
    private const int ExtraSpellId = 101;
    private const int ChargeSpellId = 200;
    private const int OnCooldownSpellId = 300;
    private const int AsNeededSpellId = 400;
    private const int HeldChargeSpellId = 500;

    [Fact]
    public void Segments_CoverTheWindowEndToEndWithNoGaps()
    {
        var lane = Single(
            [Cast(SpellId, 2000), Begin(SpellId, 2000), End(SpellId, 17_000)],
            [Pull(0, 0, 40_000)],
            0,
            40_000);

        var cursor = 0;
        foreach (var segment in lane.Segments)
        {
            segment.Start.ShouldBe(cursor);
            cursor = segment.End;
        }

        cursor.ShouldBe(40_000);
    }

    [Fact]
    public void ARechargeBecomesARechargingSegmentBoundedByWhenTheChargeCameBack()
    {
        var lane = Single(
            [Cast(SpellId, 2000), Begin(SpellId, 2000), End(SpellId, 9000)],
            [Pull(0, 0, 40_000)],
            0,
            40_000);

        var recharging = lane.Segments.Single(s => s.State == CooldownGraphState.Recharging);
        recharging.Start.ShouldBe(2000);
        recharging.End.ShouldBe(9000);
    }

    [Fact]
    public void AHoldShorterThanTheRecharge_IsAvailableRatherThanAUseLost()
    {
        var lane = Single(
            [
                Cast(SpellId, 0), Begin(SpellId, 0), End(SpellId, 15_000),
                Cast(SpellId, 20_000), Begin(SpellId, 20_000), End(SpellId, 35_000),
            ],
            [Pull(0, 0, 35_000)],
            0,
            35_000);

        lane.Segments.ShouldNotContain(s => s.State == CooldownGraphState.UseLost);
        var hold = lane.Segments.Single(s => s.State == CooldownGraphState.Available);
        hold.Start.ShouldBe(15_000);
        hold.End.ShouldBe(20_000);
    }

    [Fact]
    public void AHoldAsLongAsTheRecharge_IsAUseLost()
    {
        var lane = Single(
            [
                Cast(SpellId, 0), Begin(SpellId, 0), End(SpellId, 15_000),
                Cast(SpellId, 30_000), Begin(SpellId, 30_000), End(SpellId, 45_000),
            ],
            [Pull(0, 0, 45_000)],
            0,
            45_000);

        var useLost = lane.Segments.Single(s => s.State == CooldownGraphState.UseLost);
        useLost.Start.ShouldBe(15_000);
        useLost.End.ShouldBe(30_000);
    }

    [Fact]
    public void AHoldSpanningTimeBetweenTwoPulls_IsRatedPerPullRatherThanEndToEnd()
    {
        var lane = Single([], [Pull(0, 0, 10_000), Pull(1, 60_000, 70_000)], 0, 70_000);

        lane.Segments.ShouldBe(
        [
            new CooldownGraphSegment(0, 10_000, CooldownGraphState.Available),
            new CooldownGraphSegment(10_000, 60_000, CooldownGraphState.OutsidePull),
            new CooldownGraphSegment(60_000, 70_000, CooldownGraphState.Available),
        ]);
    }

    [Fact]
    public void TimeOutsideEveryPull_IsExcludedFromTheDenominator()
    {
        var lane = Single(
            [Cast(SpellId, 1000), Begin(SpellId, 1000), End(SpellId, 6000)],
            [Pull(0, 0, 10_000), Pull(1, 60_000, 70_000)],
            0,
            70_000);

        lane.Efficiency!.Value.ShouldBe(5000 / 20_000d, 0.0001);
    }

    [Fact]
    public void ARechargeStraddlingAPullEnd_CountsOnlyTheTimeInsideThePull()
    {
        var lane = Single(
            [Cast(SpellId, 8000), Begin(SpellId, 8000), End(SpellId, 28_000)],
            [Pull(0, 0, 10_000), Pull(1, 60_000, 70_000)],
            0,
            70_000);

        lane.Efficiency!.Value.ShouldBe(2000 / 20_000d, 0.0001);
    }

    [Fact]
    public void MaxCasts_UsesTheObservedRechargeRatherThanTheCuratedCooldown()
    {
        var lane = Single(
            [
                Cast(SpellId, 0), Begin(SpellId, 0), End(SpellId, 5000),
                Cast(SpellId, 5000), Begin(SpellId, 5000), End(SpellId, 10_000),
            ],
            [Pull(0, 0, 40_000)],
            0,
            40_000);

        lane.Casts.ShouldBe(2);
        lane.MaxCasts.ShouldBe(9);
    }

    [Fact]
    public void ARechargeCompletingOutsideThePull_DoesNotShortenTheObservedPeriod()
    {
        var lane = Single(
            [Cast(SpellId, 35_000), Begin(SpellId, 35_000), End(SpellId, 55_000)],
            [Pull(0, 0, 40_000)],
            0,
            40_000);

        lane.MaxCasts.ShouldBe(3);
    }

    [Fact]
    public void AnAbilityNeverCast_ReportsNoCastsAndAUseLostWindow()
    {
        var lane = Single([], [Pull(0, 0, 40_000)], 0, 40_000);

        lane.Casts.ShouldBe(0);
        lane.Efficiency!.Value.ShouldBe(0);
        lane.Performance.ShouldBe(QualitativePerformance.Fail);
        lane.Segments.Single().ShouldBe(new CooldownGraphSegment(0, 40_000, CooldownGraphState.UseLost));
    }

    [Fact]
    public void CastsAreReadFromTheAbilitysWholeSpellFamily()
    {
        var lane = Single(
            [Cast(ExtraSpellId, 3000), Begin(ExtraSpellId, 3000), End(ExtraSpellId, 18_000)],
            [Pull(0, 0, 40_000)],
            0,
            40_000);

        lane.CastTimestamps.ShouldBe([3000]);
        lane.Casts.ShouldBe(1);
    }

    [Fact]
    public void ACastOutsideEveryPull_IsDrawnButNotCounted()
    {
        var lane = Single(
            [Cast(SpellId, 30_000), Begin(SpellId, 30_000), End(SpellId, 45_000)],
            [Pull(0, 0, 10_000), Pull(1, 60_000, 70_000)],
            0,
            70_000);

        lane.CastTimestamps.ShouldBe([30_000]);
        lane.Casts.ShouldBe(0);
    }

    [Fact]
    public void AChargeAbilityDefaultsToMarkingEveryHold()
    {
        var lane = Build(
            [Cast(ChargeSpellId, 0), Begin(ChargeSpellId, 0), End(ChargeSpellId, 15_000)],
            [Pull(0, 0, 20_000)],
            0,
            20_000)[1];

        lane.Segments.ShouldNotContain(s => s.State == CooldownGraphState.Available);
        lane.Segments.Single(s => s.State == CooldownGraphState.Held).ShouldBe(
            new CooldownGraphSegment(15_000, 20_000, CooldownGraphState.Held));
    }

    [Fact]
    public void AnAbilityCastOnCooldown_MarksAHoldShorterThanTheRecharge()
    {
        var lane = Build(
            [
                Cast(OnCooldownSpellId, 0), Begin(OnCooldownSpellId, 0), End(OnCooldownSpellId, 15_000),
                Cast(OnCooldownSpellId, 20_000), Begin(OnCooldownSpellId, 20_000), End(OnCooldownSpellId, 35_000),
            ],
            [Pull(0, 0, 35_000)],
            0,
            35_000)[2];

        lane.Segments.ShouldNotContain(s => s.State == CooldownGraphState.Available);
        lane.Segments.Single(s => s.State == CooldownGraphState.Held).ShouldBe(
            new CooldownGraphSegment(15_000, 20_000, CooldownGraphState.Held));
    }

    [Fact]
    public void AnAbilityCastAsNeeded_MarksNoHoldEvenPastAFullRecharge()
    {
        var lane = Build(
            [
                Cast(AsNeededSpellId, 0), Begin(AsNeededSpellId, 0), End(AsNeededSpellId, 15_000),
                Cast(AsNeededSpellId, 30_000), Begin(AsNeededSpellId, 30_000), End(AsNeededSpellId, 45_000),
            ],
            [Pull(0, 0, 45_000)],
            0,
            45_000)[3];

        lane.Segments.ShouldNotContain(s => s.State == CooldownGraphState.Held || s.State == CooldownGraphState.UseLost);
        lane.Segments.Single(s => s.State == CooldownGraphState.Available).ShouldBe(
            new CooldownGraphSegment(15_000, 30_000, CooldownGraphState.Available));
    }

    [Fact]
    public void TheSpellbooksOwnUsage_OverridesTheChargesDefault()
    {
        var lane = Build(
            [Cast(HeldChargeSpellId, 0), Begin(HeldChargeSpellId, 0), End(HeldChargeSpellId, 15_000)],
            [Pull(0, 0, 40_000)],
            0,
            40_000)[4];

        lane.Segments.ShouldNotContain(s => s.State == CooldownGraphState.Held || s.State == CooldownGraphState.UseLost);
        lane.Segments.Single(s => s.State == CooldownGraphState.Available).ShouldBe(
            new CooldownGraphSegment(15_000, 40_000, CooldownGraphState.Available));
    }

    [Fact]
    public void ReachingMaxCasts_ReadsAsFullEfficiencyEvenWhileTheAbilityWasAvailable()
    {
        var lane = Single(
            [
                Cast(SpellId, 0), Begin(SpellId, 0, 5000), End(SpellId, 5000),
                Cast(SpellId, 6000), Begin(SpellId, 6000, 5000), End(SpellId, 11_000),
                Cast(SpellId, 11_000), Begin(SpellId, 11_000, 5000),
            ],
            [Pull(0, 0, 12_000)],
            0,
            12_000);

        lane.Casts.ShouldBe(3);
        lane.MaxCasts.ShouldBe(3);
        lane.Segments.ShouldContain(new CooldownGraphSegment(5000, 6000, CooldownGraphState.Available));
        lane.Efficiency!.Value.ShouldBeLessThan(1);
        lane.EffectiveEfficiency!.Value.ShouldBe(1);
    }

    [Fact]
    public void TheSpellbooksOwnTargetOverridesTheDefault()
    {
        var lane = CooldownGraphModel.Build(
            [.. new TargetedSpellbook().GetAbilities()],
            [Cast(SpellId, 0), Begin(SpellId, 0), End(SpellId, 15_000)],
            new TargetedSpellbook(),
            PlayerId,
            [Pull(0, 0, 30_000)],
            0,
            30_000).Single();

        lane.Efficiency!.Value.ShouldBe(0.5, 0.0001);
        lane.Performance.ShouldBe(QualitativePerformance.Perfect);
    }

    [Fact]
    public void AWindowWithNoPulls_ProducesNoLane()
    {
        CooldownGraphModel.Build(
            [.. new TestSpellbook().GetAbilities()],
            [],
            new TestSpellbook(),
            PlayerId,
            [],
            0,
            0).ShouldBeEmpty();
    }

    private static CooldownGraphLane Single(
        List<Event> events, List<PullStartEvent> pulls, int windowStart, int windowEnd) =>
        Build(events, pulls, windowStart, windowEnd)[0];

    private static List<CooldownGraphLane> Build(
        List<Event> events, List<PullStartEvent> pulls, int windowStart, int windowEnd)
    {
        var spellbook = new TestSpellbook();
        return CooldownGraphModel.Build(
            [.. spellbook.GetAbilities()], events, spellbook, PlayerId, pulls, windowStart, windowEnd);
    }

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

    private static CastEvent Cast(int spellId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = "Spell" },
    };

    private static UpdateSpellUsableEvent Begin(int spellId, int timestamp, int rechargeMs = 15_000) =>
        Update(UpdateSpellUsableType.BeginCooldown, spellId, timestamp, timestamp, timestamp + rechargeMs, true);

    private static UpdateSpellUsableEvent End(int spellId, int timestamp) =>
        Update(UpdateSpellUsableType.EndCooldown, spellId, timestamp, timestamp, timestamp, false);

    private static UpdateSpellUsableEvent Update(
        UpdateSpellUsableType type,
        int spellId,
        int timestamp,
        int chargeStart,
        int expectedRecharge,
        bool onCooldown) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            UpdateType = type,
            IsOnCooldown = onCooldown,
            ChargeStartTimestamp = chargeStart,
            OverallStartTimestamp = chargeStart,
            ExpectedRechargeTimestamp = expectedRecharge,
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
                PrimarySpell = new Spell { Id = ChargeSpellId, Name = "Charged Spell", Cooldown = 20, Charges = 2 },
                Category = SpellCategory.Cooldowns,
            },
            new()
            {
                PrimarySpell = new Spell { Id = OnCooldownSpellId, Name = "On Cooldown Spell", Cooldown = 15 },
                Category = SpellCategory.Cooldowns,
                CastEfficiency = new CastEfficiencyInfo { Usage = CooldownUsage.OnCooldown },
            },
            new()
            {
                PrimarySpell = new Spell { Id = AsNeededSpellId, Name = "As Needed Spell", Cooldown = 15 },
                Category = SpellCategory.Cooldowns,
                CastEfficiency = new CastEfficiencyInfo { Usage = CooldownUsage.AsNeeded },
            },
            new()
            {
                PrimarySpell = new Spell { Id = HeldChargeSpellId, Name = "Held Charge Spell", Cooldown = 20, Charges = 2 },
                Category = SpellCategory.Cooldowns,
                CastEfficiency = new CastEfficiencyInfo { Usage = CooldownUsage.AsNeeded },
            },
        ];
    }

    private sealed class TargetedSpellbook : Abilities
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

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests that <see cref="SpellUsable"/> keeps the exact cooldown progress of each ability between events,
/// fixes the length of a chain at the spend from full charges, and resyncs on a cast the log has.
/// </summary>
public sealed partial class SpellUsableTests
{
    private static readonly int Standard = StatTrackerCooldownTests.StandardSpell.Id;
    private static readonly int Charged = StatTrackerCooldownTests.ChargedSpell.Id;
    private static readonly int Static = StatTrackerCooldownTests.StaticSpell.Id;

    private static List<UpdateSpellUsableEvent> UpdatesOf(UpdateProbeModule probe, int spellId, UpdateSpellUsableType updateType) =>
        [.. probe.Updates.Where(e => e.Ability.FSLID == spellId && e.UpdateType == updateType)];

    private static ApplyBuffEvent CreateFiller(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = 9999, Name = "Filler" },
    };

    private static RemoveBuffEvent CreateHasteBuffRemoval(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = HasteBuffId, Name = "Haste Buff" },
    };

    private static Action<CombatLogParser, ApplyBuffEvent> RecoveryModifierOnTriggers(CooldownModifier modifier) =>
        (owner, e) =>
        {
            var statTracker = owner.GetModule<StatTracker>()!;
            if (e.Ability?.FSLID.Value == TriggerId)
                statTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, modifier, e);
            if (e.Ability?.FSLID.Value == RemoveTriggerId)
                statTracker.RemoveCooldownModifier(CooldownPool.CooldownRecovery, modifier, e);
        };

    [Fact]
    public async Task SingleCharge_ReturnsAtTheBaseCooldown()
    {
        var (_, _, probe) = await Run([CreateCast(1000, Standard), CreateFiller(40_000)]);

        Assert.Equal(31_000, Assert.Single(UpdatesOf(probe, Standard, UpdateSpellUsableType.EndCooldown)).Timestamp);
    }

    [Fact]
    public async Task RateChange_ChargeReturnsInsideTheChangedSpan()
    {
        var (_, _, probe) = await Run(
            [CreateCast(0, Standard), CreateTrigger(10_000), CreateTrigger(13_000, RemoveTriggerId), CreateFiller(40_000)],
            onApplyBuff: RecoveryModifierOnTriggers(new CooldownModifier(8.0)));

        Assert.Equal(12_500, Assert.Single(UpdatesOf(probe, Standard, UpdateSpellUsableType.EndCooldown)).Timestamp);
    }

    [Fact]
    public async Task RateChange_ChargeReturnsAfterTheChangedSpan()
    {
        var (_, _, probe) = await Run(
            [CreateCast(0, Standard), CreateTrigger(10_000), CreateTrigger(11_000, RemoveTriggerId), CreateFiller(40_000)],
            onApplyBuff: RecoveryModifierOnTriggers(new CooldownModifier(8.0)));

        Assert.Equal(23_000, Assert.Single(UpdatesOf(probe, Standard, UpdateSpellUsableType.EndCooldown)).Timestamp);
    }

    [Fact]
    public async Task RepeatedHasteChanges_KeepTheProgressAlreadyMade()
    {
        var (_, _, probe) = await Run(
        [
            CreateCast(0, Charged),
            CreateCast(0, Charged),
            CreateHasteBuff(1000),
            CreateHasteBuffRemoval(2000),
            CreateHasteBuff(3000),
            CreateHasteBuffRemoval(4000),
            CreateFiller(40_000),
        ]);

        Assert.Equal(11_400, UpdatesOf(probe, Charged, UpdateSpellUsableType.RestoreCharge)[0].Timestamp);
    }

    [Fact]
    public async Task FixedRechargeLength_EveryChargeOfTheChainReusesTheLengthOfTheFirstSpend()
    {
        var (_, spellUsable, probe) = await Run(
        [
            CreateHasteBuff(0),
            CreateCast(0, ChargedDurationHasteId),
            CreateHasteBuffRemoval(1000),
            CreateCast(2000, ChargedDurationHasteId),
            CreateFiller(40_000),
        ]);

        Assert.Equal(15_385, Assert.Single(UpdatesOf(probe, ChargedDurationHasteId, UpdateSpellUsableType.RestoreCharge)).Timestamp);
        Assert.Equal(30_770, Assert.Single(UpdatesOf(probe, ChargedDurationHasteId, UpdateSpellUsableType.EndCooldown)).Timestamp);
        Assert.Equal(15_384.615, spellUsable.StateOf(ChargedDurationHasteId)!.RechargeLength, precision: 3);
    }

    [Fact]
    public async Task Resync_SingleCharge_ReturnsTheChargeAtTheLoggedCast()
    {
        var (parser, spellUsable, probe) = await Run([CreateCast(0, Standard), CreateCast(29_000, Standard), CreateFiller(70_000)]);

        Assert.Equal(new CooldownDrift(Standard, 29_000, 1000), Assert.Single(spellUsable.Drifts));
        Assert.Single(NoChargeAnnotations(parser));
        Assert.Contains(UpdatesOf(probe, Standard, UpdateSpellUsableType.EndCooldown), e => e.Timestamp == 59_000);
    }

    [Fact]
    public async Task Resync_InsideTheLagMargin_AddsADriftAndNoAnnotation()
    {
        var (parser, spellUsable, _) = await Run([CreateCast(0, Standard), CreateCast(29_900, Standard)]);

        Assert.Equal(100, Assert.Single(spellUsable.Drifts).Milliseconds);
        Assert.Empty(NoChargeAnnotations(parser));
    }

    [Fact]
    public async Task Resync_Charges_ReturnsOneChargeAndKeepsTheChainRecharge()
    {
        var chargesAfterThirdCast = -1;

        var (_, spellUsable, probe) = await Run(
            [CreateCast(0, Charged), CreateCast(100, Charged), CreateCast(5000, Charged), CreateTrigger(6000), CreateFiller(40_000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    chargesAfterThirdCast = owner.GetModule<SpellUsable>()!.ChargesAvailable(Charged);
            });

        Assert.Equal(7000, Assert.Single(spellUsable.Drifts).Milliseconds);
        Assert.Equal(0, chargesAfterThirdCast);
        Assert.Contains(UpdatesOf(probe, Charged, UpdateSpellUsableType.RestoreCharge), e => e.Timestamp == 17_000);
    }

    [Fact]
    public async Task Resync_CastAtTheReturnInstant_HasNoDrift()
    {
        var (_, spellUsable, probe) = await Run([CreateCast(0, Standard), CreateCast(30_000, Standard), CreateFiller(70_000)]);

        Assert.All(spellUsable.Drifts, drift => Assert.Equal(0, drift.Milliseconds));
        Assert.Contains(UpdatesOf(probe, Standard, UpdateSpellUsableType.EndCooldown), e => e.Timestamp == 60_000);
    }

    [Fact]
    public async Task Pulls_ARechargeContinuesThroughTheTimeBetweenPulls()
    {
        var firstPull = CreatePull(5000, 60_000, index: 0);
        var secondPull = CreatePull(150_000, 180_000, index: 1);

        var (_, spellUsable, probe) = await Run(
        [
            firstPull,
            CreateCast(10_000, LongStandardId),
            firstPull.End,
            secondPull,
            secondPull.End,
            CreateFiller(200_000),
        ]);

        Assert.Equal(190_000, Assert.Single(UpdatesOf(probe, LongStandardId, UpdateSpellUsableType.EndCooldown)).Timestamp);
        Assert.True(spellUsable.IsAvailable(LongStandardId));
    }

    [Fact]
    public async Task AdditionalSpellId_SpendsAChargeOfThePrimarySpell()
    {
        var (_, spellUsable, probe) = await Run(
            [CreateCast(0, TwoChargeId), CreateCast(1000, TwoChargeAdditionalId), CreateTrigger(2000)]);

        Assert.Equal(0, spellUsable.ChargesAvailable(TwoChargeId));
        Assert.DoesNotContain(probe.Updates, e => e.Ability.FSLID == TwoChargeAdditionalId);
        Assert.Contains(probe.Updates, e => e.Ability.FSLID == TwoChargeId);
    }

    [Fact]
    public async Task ReduceCooldown_TakesCooldownProgress_NotTimeAtTheRecoveryRate()
    {
        var reduction = default(CooldownReductionResult);

        var (_, _, probe) = await Run(
            [CreateTrigger(0), CreateCast(0, Standard), CreateTrigger(0, RemoveTriggerId), CreateFiller(40_000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(CooldownPool.CooldownRecovery, new CooldownModifier(2.0), e);
                if (e.Ability?.FSLID.Value == RemoveTriggerId)
                    reduction = owner.GetModule<SpellUsable>()!.ReduceCooldown(Standard, 10_000, e.Timestamp);
            });

        Assert.Equal(10_000, reduction.Total);
        Assert.Equal(10_000, reduction.Effective);
        Assert.Equal(10_000, Assert.Single(UpdatesOf(probe, Standard, UpdateSpellUsableType.EndCooldown)).Timestamp);
    }

    [Fact]
    public async Task ReduceCooldown_OnAStaticCooldown_ShortensItByTheRequest()
    {
        var (_, spellUsable, _) = await Run([CreateCast(0, Static)]);

        spellUsable.ReduceCooldown(Static, 4000, 1000);

        Assert.Equal(11_000, spellUsable.CooldownRemaining(Static, 1000));
    }

    [Fact]
    public async Task RefundCharge_KeepsTheRechargeInProgress()
    {
        var (_, spellUsable, _) = await Run([CreateCast(0, Charged), CreateCast(0, Charged)]);

        Assert.True(spellUsable.RefundCharge(Charged, 5000));

        Assert.Equal(1, spellUsable.ChargesAvailable(Charged));
        Assert.Equal(7000, spellUsable.CooldownRemaining(Charged, 5000));
    }

    private static PullStartEvent CreatePull(int start, int end, int index) => new()
    {
        Timestamp = start,
        End = new PullEndEvent { Timestamp = end },
        Index = index,
        Id = index + 1,
        Name = "Test Pull",
        Targets = PullKind.Single,
        IsBoss = true,
        Kill = false,
    };
}

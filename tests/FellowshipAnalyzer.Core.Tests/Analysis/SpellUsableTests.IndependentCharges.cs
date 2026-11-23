using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="SpellbookAbility.IndependentCharges"/>: each charge of SpellE recharges on its own
/// 20 s timer from the cast that spent it.
/// </summary>
public sealed partial class SpellUsableTests
{
    /// <summary>
    /// Casts at t=1000 and t=6000 return at t=21000 and t=26000. Between the two returns the one charge left
    /// recharging is the second cast's.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_EachCastReturnsOneRechargeAfterItself()
    {
        var chargesBetweenReturns = -1;
        var remainingBetweenReturns = -1;

        var (_, _, probe) = await Run(
            [CreateCast(1000, SpellE), CreateCast(6000, SpellE), CreateTrigger(23_000), CreateTrigger(30_000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Timestamp != 23_000) return;
                var spellUsable = owner.GetModule<SpellUsable>()!;
                chargesBetweenReturns = spellUsable.ChargesAvailable(SpellE);
                remainingBetweenReturns = spellUsable.CooldownRemaining(SpellE);
            });

        Assert.Equal(0, LastUpdate(probe, SpellE, UpdateSpellUsableType.UseCharge).ChargesAvailable);

        var restore = LastUpdate(probe, SpellE, UpdateSpellUsableType.RestoreCharge);
        Assert.Equal(21_000, restore.Timestamp);
        Assert.Equal(1, restore.ChargesAvailable);

        var end = LastUpdate(probe, SpellE, UpdateSpellUsableType.EndCooldown);
        Assert.Equal(26_000, end.Timestamp);
        Assert.Equal(2, end.ChargesAvailable);

        Assert.Equal(1, chargesBetweenReturns);
        Assert.Equal(3000, remainingBetweenReturns);
    }

    /// <summary>
    /// The first charge is back at t=21000. A cast at t=22000 starts a third timer ending at t=42000, and the
    /// second timer still ends at t=26000.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_CastAfterFirstReturn_LeavesTheSecondTimerUntouched()
    {
        var (_, _, probe) = await Run(
            [CreateCast(1000, SpellE), CreateCast(6000, SpellE), CreateCast(22_000, SpellE), CreateTrigger(45_000)]);

        Assert.Equal(26_000, LastUpdate(probe, SpellE, UpdateSpellUsableType.UseCharge).ExpectedRechargeTimestamp);
        Assert.Equal(42_000, LastUpdate(probe, SpellE, UpdateSpellUsableType.EndCooldown).Timestamp);
    }

    /// <summary>
    /// Timers ending at t=21000 and t=26000 have 13000 and 18000 left at t=8000. A 1.0 acceleration modifier
    /// added there doubles the rate, so they end at t=14500 and t=17000.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_AccelerationChange_RescalesEveryTimer()
    {
        var (_, _, probe) = await Run(
            [CreateCast(1000, SpellE), CreateCast(6000, SpellE), CreateTrigger(8000), CreateTrigger(30_000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId && e.Timestamp == 8000)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration, new CooldownModifier(1.0), e);
            });

        Assert.Equal(14_500, LastUpdate(probe, SpellE, UpdateSpellUsableType.ChangeCooldownRate).ExpectedRechargeTimestamp);

        var restore = LastUpdate(probe, SpellE, UpdateSpellUsableType.RestoreCharge);
        Assert.Equal(14_500, restore.Timestamp);
        Assert.Equal(17_000, restore.ExpectedRechargeTimestamp);
        Assert.Equal(17_000, LastUpdate(probe, SpellE, UpdateSpellUsableType.EndCooldown).Timestamp);
    }

    /// <summary>
    /// Timers ending at t=20000 and t=25000 take a 3000 ms reduction at t=18000: the earliest completes with
    /// its 2000 ms, and the other 1000 ms shortens the second timer to t=24000.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_ReduceCooldown_CompletesTheEarliestTimerAndShortensTheNext()
    {
        var (_, spellUsable, _) = await Run([]);
        spellUsable.BeginCooldown(SpellE, timestamp: 0);
        spellUsable.BeginCooldown(SpellE, timestamp: 5000);

        var cdr = spellUsable.ReduceCooldown(SpellE, 3000, timestamp: 18_000);

        Assert.Equal(3000, cdr.Effective);
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellE));
        Assert.Equal(6000, spellUsable.CooldownRemaining(SpellE, atTimestamp: 18_000));
    }

    /// <summary>
    /// An acceleration of 0.0009999999 added at t=1000 leaves 17999.0000001 ms of progress at t=2000. A
    /// reduction of 17999 ms there leaves 0.0000001 ms, so the timer's own return is due at t=2000 and arrives
    /// with no time since the state last advanced. The timer is complete and its charge returns once.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_TimerDueAtTheInstantItWasAdvanced_Completes()
    {
        var (_, spellUsable, probe) = await Run(
            [CreateCast(0, SpellE), CreateTrigger(1000), CreateTrigger(2000, RemoveTriggerId), CreateTrigger(5000, 9999)],
            onApplyBuff: (owner, e) =>
            {
                var module = owner.GetModule<SpellUsable>()!;
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration, new CooldownModifier(0.0009999999), e);
                if (e.Ability?.FSLID.Value == RemoveTriggerId)
                    module.ReduceCooldown(SpellE, 17_999);
            });

        Assert.False(spellUsable.IsOnCooldown(SpellE));
        Assert.Equal(2, spellUsable.ChargesAvailable(SpellE));
        Assert.Equal(1, probe.Updates.Count(e => e.Ability.FSLID == SpellE && e.UpdateType == UpdateSpellUsableType.EndCooldown));
    }

    /// <summary>
    /// Timers ending at t=21000 and t=26000 are ended by force at t=10000. The EndCooldown for the ended
    /// ability has nothing left to recharge, so its expected recharge is the event's own timestamp with a
    /// duration of 0.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_EndCooldownOfEveryTimer_HasNoFutureExpectedRecharge()
    {
        var (_, _, probe) = await Run(
            [CreateCast(1000, SpellE), CreateCast(6000, SpellE), CreateTrigger(10_000), CreateTrigger(30_000, 9999)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<SpellUsable>()!.EndCooldown(SpellE, restoreAllCharges: true);
            });

        var end = LastUpdate(probe, SpellE, UpdateSpellUsableType.EndCooldown);
        Assert.Equal(10_000, end.Timestamp);
        Assert.Equal(10_000, end.ExpectedRechargeTimestamp);
        Assert.Equal(0, end.ExpectedRechargeDuration);
        Assert.Equal(2, end.ChargesAvailable);
    }

    /// <summary>
    /// Timers from casts at t=1000 and t=6000 end at t=21000 and t=26000. A refund at t=10000 drops the second
    /// cast's timer, and the first cast's charge still returns at t=21000.
    /// </summary>
    [Fact]
    public async Task IndependentCharges_RefundCharge_DropsTheLatestTimer()
    {
        var (_, spellUsable, _) = await Run([]);
        spellUsable.BeginCooldown(SpellE, timestamp: 1000);
        spellUsable.BeginCooldown(SpellE, timestamp: 6000);

        Assert.True(spellUsable.RefundCharge(SpellE, timestamp: 10_000));
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellE));
        Assert.Equal(11_000, spellUsable.CooldownRemaining(SpellE, atTimestamp: 10_000));
    }
}

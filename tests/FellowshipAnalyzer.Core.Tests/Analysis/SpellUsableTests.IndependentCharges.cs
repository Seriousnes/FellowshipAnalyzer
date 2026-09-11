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
}

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="SpellbookAbility.CooldownStartsWhenBuffEnds"/>: an ability whose recharge waits
/// for its own buff to leave the player, as Aeona's Fleeting Hour does. The cast consumes the charge and
/// holds; the recharge begins on the buff's removal, at the rate current then.
/// </summary>
public sealed partial class SpellUsableTests
{
    /// <summary>
    /// SpellD is cast at t=1000 and its buff runs to t=16000. The 20 s recharge starts on the removal,
    /// not the cast, so the ability comes back at t=36000 rather than t=21000.
    /// </summary>
    [Fact]
    public async Task DeferredStart_BeginsRechargeOnBuffRemoval()
    {
        var (_, _, probe) = await Run(
        [
            CreateCast(1000, SpellD),
            CreateDeferredBuffRemoval(16_000),
        ]);

        var begin = probe.Updates.Last(e =>
            e.Ability.FSLID == SpellD && e.UpdateType == UpdateSpellUsableType.BeginCooldown);

        Assert.Equal(16_000, begin.ChargeStartTimestamp);
        Assert.Equal(36_000, begin.ExpectedRechargeTimestamp);
    }

    /// <summary>While the buff is up the ability is unavailable and nothing is recharging.</summary>
    [Fact]
    public async Task DeferredStart_HoldsWhileTheBuffIsUp()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellD)]);

        Assert.False(spellUsable.IsAvailable(SpellD));
        Assert.True(spellUsable.IsOnCooldown(SpellD));
    }

    /// <summary>
    /// A held ability reports its full recharge duration as the time still to come, so nothing reads it
    /// as available while the buff is up.
    /// </summary>
    [Fact]
    public async Task DeferredStart_ReportsTheFullRechargeWhileHeld()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellD)]);

        Assert.Equal(20_000, spellUsable.CooldownRemaining(SpellD, 30_000));
    }

    /// <summary>
    /// A cooldown reduction cannot shorten a hold, because nothing is counting down: Temporal Barrage
    /// bolts cast inside a Fleeting Hour window must not advance its recharge.
    /// </summary>
    [Fact]
    public async Task DeferredStart_IgnoresCooldownReductionWhileHeld()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellD)]);

        var reduction = spellUsable.ReduceCooldown(SpellD, 5000, 2000);

        Assert.Equal(0, reduction.Effective);
        Assert.False(spellUsable.IsAvailable(SpellD));
    }

    /// <summary>Once the recharge is running, a reduction shortens it as it does any other cooldown.</summary>
    [Fact]
    public async Task DeferredStart_AcceptsCooldownReductionOnceRecharging()
    {
        var (_, spellUsable, _) = await Run(
        [
            CreateCast(1000, SpellD),
            CreateDeferredBuffRemoval(16_000),
        ]);

        var reduction = spellUsable.ReduceCooldown(SpellD, 5000, 20_000);

        Assert.Equal(5000, reduction.Effective);
        Assert.Equal(15_000, spellUsable.CooldownRemaining(SpellD, 16_000));
    }

    /// <summary>The ability is available again once the recharge that started on the removal completes.</summary>
    [Fact]
    public async Task DeferredStart_BecomesAvailableAfterTheRecharge()
    {
        var (_, spellUsable, _) = await Run(
        [
            CreateCast(1000, SpellD),
            CreateDeferredBuffRemoval(16_000),
            CreateCast(40_000, SpellA),
        ]);

        Assert.True(spellUsable.IsAvailable(SpellD));
    }

    private static RemoveBuffEvent CreateDeferredBuffRemoval(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = DeferredBuff.FSLID, Name = DeferredBuff.Name },
    };
}

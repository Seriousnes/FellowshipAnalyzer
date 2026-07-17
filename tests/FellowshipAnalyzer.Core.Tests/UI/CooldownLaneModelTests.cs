using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Timeline;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

/// <summary>
/// Tests for <see cref="CooldownLaneModel"/>: recharge bars end at the actual charge-restore
/// timestamp, not the (possibly stale) expected recharge, so dynamic cooldown reductions render
/// correctly instead of drawing full-length bars with a restore landing mid-bar.
/// </summary>
public sealed class CooldownLaneModelTests
{
    [Fact]
    public void RestoreBeforeExpectedRecharge_BarEndsAtActualRestore()
    {
        // Cast at 1000 with a nominal 20s recharge, but the charge is restored at 6000 (a dynamic
        // reduction shortened it). The bar must end at 6000, not the stale 21000.
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 6000, chargeStart: 6000, expEnd: 26000, onCd: false),
        };

        var (segments, restores) = CooldownLaneModel.Build(events, fightEndTime: 60_000);

        var bar = Assert.Single(segments, s => s.End > s.Start);
        Assert.Equal(1000, bar.Start);
        Assert.Equal(6000, bar.End);
        Assert.Equal([6000], restores);
    }

    [Fact]
    public void TwoChargeSpam_ProducesSequentialNonOverlappingBars()
    {
        // Both charges used, then restored one at a time. Each recharge bar spans from its start to
        // the next restore/end, so the bars are sequential and never overlap.
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.UseCharge, ts: 2000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 6000, chargeStart: 6000, expEnd: 26000, onCd: true),
            Update(UpdateSpellUsableType.EndCooldown, ts: 9000, chargeStart: 6000, expEnd: 26000, onCd: false),
        };

        var (segments, _) = CooldownLaneModel.Build(events, fightEndTime: 60_000);

        var bars = segments.Where(s => s.End > s.Start).ToList();
        Assert.Equal(2, bars.Count);
        Assert.Equal((1000, 6000), (bars[0].Start, bars[0].End));
        Assert.Equal((6000, 9000), (bars[1].Start, bars[1].End));
        Assert.True(bars[1].Start >= bars[0].End, "bars must not overlap");
    }

    [Fact]
    public void CastsRenderIconMarkers()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.UseCharge, ts: 2000, chargeStart: 1000, expEnd: 21000, onCd: true),
        };

        var (segments, _) = CooldownLaneModel.Build(events, fightEndTime: 60_000);

        var icons = segments.Where(s => s.ShowIcon).Select(s => s.IconAt).ToList();
        Assert.Equal([1000, 2000], icons);
    }

    [Fact]
    public void RechargeOpenAtFightEnd_ClampsToExpectedRechargeThenFightEnd()
    {
        // Recharge whose expected completion is before fight end closes at the expected time
        // (a post-combat completion should not render as an open bar to the fight end).
        var beforeEnd = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 5000, onCd: true),
        };
        var (segmentsBefore, _) = CooldownLaneModel.Build(beforeEnd, fightEndTime: 60_000);
        Assert.Equal(5000, Assert.Single(segmentsBefore, s => s.End > s.Start).End);

        // Recharge still in progress at fight end clamps to the fight end.
        var pastEnd = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
        };
        var (segmentsPast, _) = CooldownLaneModel.Build(pastEnd, fightEndTime: 10_000);
        Assert.Equal(10_000, Assert.Single(segmentsPast, s => s.End > s.Start).End);
    }

    private static UpdateSpellUsableEvent Update(
        UpdateSpellUsableType type, int ts, int chargeStart, int expEnd, bool onCd) => new()
    {
        Timestamp = ts,
        UpdateType = type,
        ChargeStartTimestamp = chargeStart,
        ExpectedRechargeTimestamp = expEnd,
        IsOnCooldown = onCd,
        Ability = new Ability { FSLID = 1, Name = "Spell" },
    };
}

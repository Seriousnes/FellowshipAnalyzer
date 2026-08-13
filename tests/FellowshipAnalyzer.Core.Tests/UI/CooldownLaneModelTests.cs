using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Timeline;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

/// <summary>
/// Tests for <see cref="CooldownLaneModel"/>: recharge bars end at the actual charge-restore
/// timestamp, not the (possibly stale) expected recharge, and every emitted element is clipped to
/// the render window <c>[windowStart, windowEnd]</c> so a single-pull view crops correctly.
/// </summary>
public sealed class CooldownLaneModelTests
{
    [Fact]
    public void RestoreBeforeExpectedRecharge_BarEndsAtActualRestore()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 6000, chargeStart: 6000, expEnd: 26000, onCd: false),
        };

        var (segments, restores) = CooldownLaneModel.Build(events, windowStart: 0, windowEnd: 60_000);

        var bar = Assert.Single(segments, s => s.End > s.Start);
        Assert.Equal(1000, bar.Start);
        Assert.Equal(6000, bar.End);
        Assert.Equal([6000], restores);
    }

    [Fact]
    public void TwoChargeSpam_ProducesSequentialNonOverlappingBars()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.UseCharge, ts: 2000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 6000, chargeStart: 6000, expEnd: 26000, onCd: true),
            Update(UpdateSpellUsableType.EndCooldown, ts: 9000, chargeStart: 6000, expEnd: 26000, onCd: false),
        };

        var (segments, _) = CooldownLaneModel.Build(events, windowStart: 0, windowEnd: 60_000);

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

        var (segments, _) = CooldownLaneModel.Build(events, windowStart: 0, windowEnd: 60_000);

        var icons = segments.Where(s => s.ShowIcon).Select(s => s.IconAt).ToList();
        Assert.Equal([1000, 2000], icons);
    }

    [Fact]
    public void RechargeOpenAtDungeonEnd_ClampsToExpectedRechargeThenWindowEnd()
    {
        var beforeEnd = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 5000, onCd: true),
        };
        var (segmentsBefore, _) = CooldownLaneModel.Build(beforeEnd, windowStart: 0, windowEnd: 60_000);
        Assert.Equal(5000, Assert.Single(segmentsBefore, s => s.End > s.Start).End);

        var pastEnd = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
        };
        var (segmentsPast, _) = CooldownLaneModel.Build(pastEnd, windowStart: 0, windowEnd: 10_000);
        Assert.Equal(10_000, Assert.Single(segmentsPast, s => s.End > s.Start).End);
    }

    [Fact]
    public void SegmentStraddlingWindowStart_ClipsToWindowStart()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 500, chargeStart: 500, expEnd: 20500, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 8000, chargeStart: 8000, expEnd: 28000, onCd: false),
        };

        var (segments, restores) = CooldownLaneModel.Build(events, windowStart: 2000, windowEnd: 60_000);

        var bar = Assert.Single(segments, s => s.End > s.Start);
        Assert.Equal(2000, bar.Start);
        Assert.Equal(8000, bar.End);
        Assert.DoesNotContain(segments, s => s.ShowIcon);
        Assert.Equal([8000], restores);
    }

    [Fact]
    public void SegmentStraddlingWindowEnd_ClipsToWindowEndAndDropsLateRestore()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 8000, chargeStart: 8000, expEnd: 28000, onCd: false),
        };

        var (segments, restores) = CooldownLaneModel.Build(events, windowStart: 0, windowEnd: 5000);

        var bar = Assert.Single(segments, s => s.End > s.Start);
        Assert.Equal(1000, bar.Start);
        Assert.Equal(5000, bar.End);
        Assert.Empty(restores);
    }

    [Fact]
    public void SegmentsFullyOutsideWindow_Dropped()
    {
        var beforeWindow = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 5000, onCd: true),
            Update(UpdateSpellUsableType.EndCooldown, ts: 5000, chargeStart: 1000, expEnd: 5000, onCd: false),
        };
        var (segmentsBefore, restoresBefore) = CooldownLaneModel.Build(beforeWindow, windowStart: 10_000, windowEnd: 20_000);
        Assert.Empty(segmentsBefore);
        Assert.Empty(restoresBefore);

        var afterWindow = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 25_000, chargeStart: 25_000, expEnd: 45_000, onCd: true),
        };
        var (segmentsAfter, _) = CooldownLaneModel.Build(afterWindow, windowStart: 10_000, windowEnd: 20_000);
        Assert.Empty(segmentsAfter);
    }

    [Fact]
    public void IconBeforeWindowStart_Suppressed()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 41000, onCd: true),
            Update(UpdateSpellUsableType.UseCharge, ts: 5000, chargeStart: 1000, expEnd: 41000, onCd: true),
        };

        var (segments, _) = CooldownLaneModel.Build(events, windowStart: 3000, windowEnd: 60_000);

        var icon = Assert.Single(segments, s => s.ShowIcon);
        Assert.Equal(5000, icon.IconAt);
    }

    [Fact]
    public void ChargeRestores_FilteredToWindow()
    {
        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 1000, chargeStart: 1000, expEnd: 21000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 1500, chargeStart: 1500, expEnd: 21500, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 6000, chargeStart: 6000, expEnd: 26000, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 12000, chargeStart: 12000, expEnd: 32000, onCd: false),
        };

        var (_, restores) = CooldownLaneModel.Build(events, windowStart: 2000, windowEnd: 10_000);

        Assert.Equal([6000], restores);
    }

    /// <summary>
    /// The lane draws every segment at its raw offset with no clipping of its own, because clipping it in
    /// CSS costs the row's alignment with the cast bar: an overflow makes the lane a block formatting
    /// context root that the floated label pushes aside. Nothing the model emits may leave the window.
    /// </summary>
    [Fact]
    public void EveryEmittedElement_StaysInsideTheWindow()
    {
        const int windowStart = 4000;
        const int windowEnd = 30_000;

        var events = new List<UpdateSpellUsableEvent>
        {
            Update(UpdateSpellUsableType.BeginCooldown, ts: 500, chargeStart: 500, expEnd: 20500, onCd: true),
            Update(UpdateSpellUsableType.UseCharge, ts: 3000, chargeStart: 500, expEnd: 20500, onCd: true),
            Update(UpdateSpellUsableType.RestoreCharge, ts: 9000, chargeStart: 9000, expEnd: 29000, onCd: true),
            Update(UpdateSpellUsableType.UseCharge, ts: 12_000, chargeStart: 9000, expEnd: 29000, onCd: true),
            Update(UpdateSpellUsableType.BeginCooldown, ts: 26_000, chargeStart: 26_000, expEnd: 46_000, onCd: true),
        };

        var (segments, restores) = CooldownLaneModel.Build(events, windowStart, windowEnd);

        segments.ShouldNotBeEmpty();

        foreach (var segment in segments)
        {
            Assert.InRange(segment.Start, windowStart, windowEnd);
            Assert.InRange(segment.End, segment.Start, windowEnd);

            if (segment.ShowIcon)
            {
                Assert.InRange(segment.IconAt, windowStart, windowEnd);
            }
        }

        foreach (var restore in restores)
        {
            Assert.InRange(restore, windowStart, windowEnd);
        }
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

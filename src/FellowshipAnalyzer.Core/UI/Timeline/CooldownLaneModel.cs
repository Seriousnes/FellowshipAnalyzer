using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.UI.Timeline;

/// <summary>A single visual element on a cooldown lane: a recharge bar (<c>End &gt; Start</c>) and/or a cast icon (<c>ShowIcon</c>).</summary>
public readonly record struct CooldownSegment(int Start, int End, int IconAt, bool ShowIcon);

/// <summary>
/// Turns a spell's <see cref="UpdateSpellUsableEvent"/> stream into cooldown-lane geometry, clipped
/// to a render window <c>[windowStart, windowEnd]</c> (the whole dungeon, or a single selected pull).
/// </summary>
/// <remarks>
/// A recharge bar spans from when a charge began recharging to when it <b>actually</b> came back:
/// the timestamp of the next <see cref="UpdateSpellUsableType.RestoreCharge"/> or
/// <see cref="UpdateSpellUsableType.EndCooldown"/>. The bar end is never taken from
/// <see cref="UpdateSpellUsableEvent.ExpectedRechargeTimestamp"/>, because dynamic cooldown
/// reductions (e.g. Rolling Flames) shorten the real recharge without restating that field, so a
/// spell that recharges in 5s would otherwise render a stale 20s bar with a restore landing mid-bar.
/// <para>
/// Every emitted element is then clipped to <c>[windowStart, windowEnd]</c>: a segment straddling an
/// edge is trimmed to it, a segment wholly outside the window is dropped, a cast icon whose timestamp
/// falls outside the window is suppressed, and charge-restore markers outside the window are removed.
/// </para>
/// </remarks>
public static class CooldownLaneModel
{
    /// <summary>Builds the recharge-bar segments and charge-restore markers for one spell's cooldown lane, clipped to <paramref name="windowStart"/>-<paramref name="windowEnd"/>.</summary>
    /// <param name="events">The spell's <see cref="UpdateSpellUsableEvent"/> stream, in timestamp order.</param>
    /// <param name="windowStart">The start of the render window, in dungeon-relative milliseconds.</param>
    /// <param name="windowEnd">The end of the render window, in dungeon-relative milliseconds.</param>
    public static (IReadOnlyList<CooldownSegment> Segments, IReadOnlyList<int> ChargeRestores) Build(
        IReadOnlyList<UpdateSpellUsableEvent> events, int windowStart, int windowEnd)
    {
        var raw = new List<CooldownSegment>();
        var chargeRestores = new List<int>();

        int? rechargeStart = null;
        var rechargeExpectedEnd = 0;

        foreach (var e in events)
        {
            switch (e.UpdateType)
            {
                case UpdateSpellUsableType.BeginCooldown:
                    raw.Add(new CooldownSegment(e.Timestamp, e.Timestamp, e.Timestamp, ShowIcon: true));
                    rechargeStart = e.ChargeStartTimestamp;
                    rechargeExpectedEnd = e.ExpectedRechargeTimestamp;
                    break;

                case UpdateSpellUsableType.UseCharge:
                    raw.Add(new CooldownSegment(e.Timestamp, e.Timestamp, e.Timestamp, ShowIcon: true));
                    break;

                case UpdateSpellUsableType.RestoreCharge:
                    chargeRestores.Add(e.Timestamp);
                    if (rechargeStart is int restoreStart)
                        raw.Add(new CooldownSegment(restoreStart, e.Timestamp, restoreStart, ShowIcon: false));
                    rechargeStart = e.IsOnCooldown ? e.Timestamp : null;
                    rechargeExpectedEnd = e.ExpectedRechargeTimestamp;
                    break;

                case UpdateSpellUsableType.EndCooldown:
                    if (rechargeStart is int endStart)
                        raw.Add(new CooldownSegment(endStart, e.Timestamp, endStart, ShowIcon: false));
                    rechargeStart = null;
                    break;

                case UpdateSpellUsableType.ChangeCooldownRate:
                    if (rechargeStart is not null)
                        rechargeExpectedEnd = e.ExpectedRechargeTimestamp;
                    break;
            }
        }

        if (rechargeStart is int openStart)
            raw.Add(new CooldownSegment(openStart, Math.Min(windowEnd, rechargeExpectedEnd), openStart, ShowIcon: false));

        var segments = new List<CooldownSegment>(raw.Count);
        foreach (var seg in raw)
        {
            var showIcon = seg.ShowIcon && seg.IconAt >= windowStart && seg.IconAt <= windowEnd;
            var start = Math.Max(seg.Start, windowStart);
            var end = Math.Min(seg.End, windowEnd);
            if (end < start || (end == start && !showIcon))
                continue;
            segments.Add(new CooldownSegment(start, end, seg.IconAt, showIcon));
        }

        var restores = new List<int>(chargeRestores.Count);
        foreach (var ts in chargeRestores)
            if (ts >= windowStart && ts <= windowEnd)
                restores.Add(ts);

        return (segments, restores);
    }
}

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.UI.Timeline;

/// <summary>A single visual element on a cooldown lane: a recharge bar (<c>End &gt; Start</c>) and/or a cast icon (<c>ShowIcon</c>).</summary>
public readonly record struct CooldownSegment(int Start, int End, int IconAt, bool ShowIcon);

/// <summary>
/// Turns a spell's <see cref="UpdateSpellUsableEvent"/> stream into cooldown-lane geometry.
/// </summary>
/// <remarks>
/// A recharge bar spans from when a charge began recharging to when it <b>actually</b> came back —
/// the timestamp of the next <see cref="UpdateSpellUsableType.RestoreCharge"/> or
/// <see cref="UpdateSpellUsableType.EndCooldown"/>. The bar end is never taken from
/// <see cref="UpdateSpellUsableEvent.ExpectedRechargeTimestamp"/>, because dynamic cooldown
/// reductions (e.g. Rolling Flames) shorten the real recharge without restating that field, so a
/// spell that recharges in 5s would otherwise render a stale 20s bar with a restore landing mid-bar.
/// </remarks>
public static class CooldownLaneModel
{
    public static (IReadOnlyList<CooldownSegment> Segments, IReadOnlyList<int> ChargeRestores) Build(
        IReadOnlyList<UpdateSpellUsableEvent> events, int fightEndTime)
    {
        var segments = new List<CooldownSegment>();
        var chargeRestores = new List<int>();

        int? rechargeStart = null;
        var rechargeExpectedEnd = 0;

        foreach (var e in events)
        {
            switch (e.UpdateType)
            {
                case UpdateSpellUsableType.BeginCooldown:
                    segments.Add(new CooldownSegment(e.Timestamp, e.Timestamp, e.Timestamp, ShowIcon: true));
                    rechargeStart = e.ChargeStartTimestamp;
                    rechargeExpectedEnd = e.ExpectedRechargeTimestamp;
                    break;

                case UpdateSpellUsableType.UseCharge:
                    segments.Add(new CooldownSegment(e.Timestamp, e.Timestamp, e.Timestamp, ShowIcon: true));
                    break;

                case UpdateSpellUsableType.RestoreCharge:
                    chargeRestores.Add(e.Timestamp);
                    if (rechargeStart is int restoreStart)
                        segments.Add(new CooldownSegment(restoreStart, e.Timestamp, restoreStart, ShowIcon: false));
                    rechargeStart = e.IsOnCooldown ? e.Timestamp : null;
                    rechargeExpectedEnd = e.ExpectedRechargeTimestamp;
                    break;

                case UpdateSpellUsableType.EndCooldown:
                    if (rechargeStart is int endStart)
                        segments.Add(new CooldownSegment(endStart, e.Timestamp, endStart, ShowIcon: false));
                    rechargeStart = null;
                    break;

                case UpdateSpellUsableType.ChangeCooldownRate:
                    if (rechargeStart is not null)
                        rechargeExpectedEnd = e.ExpectedRechargeTimestamp;
                    break;
            }
        }

        // A recharge still open at the end of the fight closes at its expected completion, clamped to
        // the fight end so a cooldown that would finish in a post-combat gap doesn't render full-length.
        if (rechargeStart is int openStart)
            segments.Add(new CooldownSegment(openStart, Math.Min(fightEndTime, rechargeExpectedEnd), openStart, ShowIcon: false));

        return (segments, chargeRestores);
    }
}

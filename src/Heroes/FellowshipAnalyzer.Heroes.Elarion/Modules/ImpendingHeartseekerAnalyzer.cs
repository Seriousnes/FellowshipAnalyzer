using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Measures the Impending Heartseeker payoff. With the talent taken, spending a Celestial Impetus
/// stack through <see cref="Spells.CelestialShot"/> also resets Heartseeker Barrage and grants the
/// <see cref="Spells.ImpendingHeartseeker"/> buff, which adds damage to every arrow of the next
/// Barrage. The buff is therefore a free Barrage waiting to be fired, and letting it fall off
/// throws the reset away.
/// <para>
/// A buff removal within <see cref="ConsumeWindowMs"/> after a Heartseeker Barrage cast is a
/// consumption and every other removal is an expiry, matched one to one so a single cast cannot
/// claim two removals. The whole analyzer is gated on the talent, so its read paths stay empty on a
/// build that does not take it.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(ElarionTalents.ImpendingHeartseeker)]
public sealed partial class ImpendingHeartseekerAnalyzer : Analyzer
{
    /// <summary>
    /// A buff removal this soon after a Heartseeker Barrage cast counts as consumed rather than
    /// expired. Matches the window <see cref="CelestialImpetusAnalyzer"/> uses for the proc that
    /// grants this one.
    /// </summary>
    public const int ConsumeWindowMs = 150;

    private readonly List<BarrageCast> _barrageCasts = [];

    /// <summary>Impending Heartseeker buffs granted during the pull, one per apply or stack-gain event.</summary>
    public int ProcsGained { get; private set; }

    /// <summary>Buffs removed by a Heartseeker Barrage cast, so the empowered Barrage was fired.</summary>
    public int ProcsConsumed { get; private set; }

    /// <summary>Buffs removed with no Heartseeker Barrage cast beside them, so the reset went unused.</summary>
    public int ProcsExpired { get; private set; }

    /// <summary>Pull length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartseekerBarrage))]
    private void OnBarrageCast(CastEvent e) => _barrageCasts.Add(new BarrageCast(e.Timestamp));

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffApplied(ApplyBuffEvent e) => ProcsGained++;

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffStackApplied(ApplyBuffStackEvent e) => ProcsGained++;

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffStackRemoved(RemoveBuffStackEvent e) => ClassifyRemoval(e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffRemoved(RemoveBuffEvent e) => ClassifyRemoval(e.Timestamp);

    private void ClassifyRemoval(int timestamp)
    {
        if (ClaimCast(timestamp))
            ProcsConsumed++;
        else
            ProcsExpired++;
    }

    private bool ClaimCast(int timestamp)
    {
        for (var i = _barrageCasts.Count - 1; i >= 0; i--)
        {
            var cast = _barrageCasts[i];
            var elapsed = timestamp - cast.Timestamp;
            if (elapsed > ConsumeWindowMs)
                break;

            if (elapsed < 0 || cast.Claimed)
                continue;

            cast.Claimed = true;
            return true;
        }

        return false;
    }

    private sealed class BarrageCast(int timestamp)
    {
        public int Timestamp { get; } = timestamp;

        public bool Claimed { get; set; }
    }
}

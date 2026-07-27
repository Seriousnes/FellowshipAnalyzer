using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(ElarionTalents.ImpendingHeartseeker)]
public sealed partial class ImpendingHeartseekerAnalyzer : Analyzer
{
    public const int ConsumeWindowMs = 150;

    private readonly List<BarrageCast> _barrageCasts = [];

    public int ProcsGained { get; private set; }

    public int ProcsConsumed { get; private set; }

    public int ProcsExpired { get; private set; }

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

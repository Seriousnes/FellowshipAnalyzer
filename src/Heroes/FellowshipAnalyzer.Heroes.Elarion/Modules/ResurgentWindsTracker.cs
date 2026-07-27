using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[RequiresTalent(ElarionTalents.ResurgentWinds)]
public sealed partial class ResurgentWindsTracker : EventSubscriber
{
    public const int ConsumeWindowMs = 150;

    public const int BeginCastToleranceMs = 5;

    private readonly List<int> _activations = [];
    private readonly List<int> _beginCasts = [];
    private readonly List<int> _procRemovals = [];

    public int ProcsGained { get; private set; }

    public int ProcsConsumed => Counted.Consumed;

    public int ProcsExpired => Counted.Expired;

    public int InstantHighwindCasts => Counted.Instants;

    public double ConsumedShare => ProcsGained == 0 ? 0d : ProcsConsumed / (double)ProcsGained;

    public double ExpiredShare => ProcsGained == 0 ? 0d : ProcsExpired / (double)ProcsGained;

    public override Type? StatisticsComponentType =>
        ProcsGained > 0 ? typeof(ResurgentWindsStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HighwindArrow))]
    private void OnHighwindCast(CastEvent e)
    {
        if (e.Activation)
            _activations.Add(e.Timestamp);
    }

    [On<BeginCastEvent>(By = Actor.Player, Spell = nameof(Spells.HighwindArrow))]
    private void OnHighwindBeginCast(BeginCastEvent e) => _beginCasts.Add(e.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ResurgentWindsBuff))]
    private void OnProcApplied(ApplyBuffEvent e) => ProcsGained++;

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ResurgentWindsBuff))]
    private void OnProcStackApplied(ApplyBuffStackEvent e) => ProcsGained++;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ResurgentWindsBuff))]
    private void OnProcRemoved(RemoveBuffEvent e) => _procRemovals.Add(e.Timestamp);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ResurgentWindsBuff))]
    private void OnProcStackRemoved(RemoveBuffStackEvent e) => _procRemovals.Add(e.Timestamp);

    private Census Counted => field ??= Count();

    private Census Count()
    {
        List<InstantCast> instants =
        [
            .. _activations
                .Where(activation => !_beginCasts.Any(begin => Math.Abs(begin - activation) <= BeginCastToleranceMs))
                .Select(timestamp => new InstantCast(timestamp)),
        ];

        var consumed = 0;
        foreach (var removal in _procRemovals)
        {
            if (Claim(instants, removal))
                consumed++;
        }

        return new Census(instants.Count, consumed, _procRemovals.Count - consumed);
    }

    private static bool Claim(List<InstantCast> instants, int timestamp)
    {
        for (var i = instants.Count - 1; i >= 0; i--)
        {
            var cast = instants[i];
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

    private sealed record Census(int Instants, int Consumed, int Expired);

    private sealed class InstantCast(int timestamp)
    {
        public int Timestamp { get; } = timestamp;

        public bool Claimed { get; set; }
    }
}

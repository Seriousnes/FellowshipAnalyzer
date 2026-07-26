using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Counts what Resurgent Winds paid out over the fight. Any offensive ability can proc
/// <see cref="Spells.ResurgentWindsBuff"/>, and the proc turns the next
/// <see cref="Spells.HighwindArrow"/> into an instant free cast, so a proc that falls off unspent is
/// a Highwind Arrow thrown away.
/// <para>
/// Fellowship logs mark the player pressing a button with <see cref="CastEvent.Activation"/>, which
/// covers both an instant cast and the start of a hardcast, so an activation alone does not identify
/// the free cast. A hardcast also logs a <see cref="BeginCastEvent"/> alongside the activation, a few
/// milliseconds either side of it, so the instant casts are the activations with no begin-cast beside
/// them. That begin-cast can arrive after the activation, so the census is computed once the fight is
/// over rather than as the events arrive.
/// </para>
/// <para>
/// A proc removal within <see cref="ConsumeWindowMs"/> after an instant Highwind Arrow is a
/// consumption and every other removal is an expiry, matched one to one so a single cast cannot claim
/// two removals. The card is withheld when the talent never procced.
/// </para>
/// </summary>
[RequiresTalent(ElarionTalents.ResurgentWinds)]
public sealed partial class ResurgentWindsTracker : EventSubscriber
{
    /// <summary>
    /// A proc removal this soon after an instant Highwind Arrow counts as consumed rather than
    /// expired. Kept tight because the game logs the removal in the same millisecond as the free cast.
    /// </summary>
    public const int ConsumeWindowMs = 150;

    /// <summary>
    /// How far a <see cref="BeginCastEvent"/> may sit from its activation and still belong to it.
    /// Fellowship logs place the pair in the same millisecond most of the time but not always.
    /// </summary>
    public const int BeginCastToleranceMs = 5;

    private readonly List<int> _activations = [];
    private readonly List<int> _beginCasts = [];
    private readonly List<int> _procRemovals = [];

    private Census? _census;

    /// <summary>Resurgent Winds procs granted during the fight, one per apply or stack-gain event.</summary>
    public int ProcsGained { get; private set; }

    /// <summary>Procs removed by an instant Highwind Arrow, so the free cast was fired.</summary>
    public int ProcsConsumed => Counted.Consumed;

    /// <summary>Procs removed with no instant Highwind Arrow beside them, so the free cast was lost.</summary>
    public int ProcsExpired => Counted.Expired;

    /// <summary>Highwind Arrow casts that went out instantly, with no cast time to start.</summary>
    public int InstantHighwindCasts => Counted.Instants;

    /// <summary>Share of procs (0-1) spent on a free Highwind Arrow.</summary>
    public double ConsumedShare => ProcsGained == 0 ? 0d : ProcsConsumed / (double)ProcsGained;

    /// <summary>Share of procs (0-1) that fell off unspent.</summary>
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

    private Census Counted => _census ??= Count();

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

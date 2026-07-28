using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// What the pull cost in mana and what the pool threw away. Mana regenerates continuously against a
/// hard cap, so a stretch spent at full is regeneration lost; that is measured by time between
/// readings rather than by counting events, because readings crowd together when Sylvie is busy.
/// <para>
/// Spending is priced by <see cref="SylvieManaTracker"/> from the game data, because the log attaches
/// no cost to a cast.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SylvieManaTracker>]
public sealed partial class ManaEfficiencyAnalyzer : Analyzer
{
    private int _lastSample;
    private int _lastAmount;
    private int _lastMax;
    private bool _seeded;

    /// <summary>Mana spent on casts this pull.</summary>
    public int Spent => SylvieManaTracker.SpentBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>Mana spent per spell this pull, heaviest first.</summary>
    public IReadOnlyList<(int SpellId, int Mana, int Casts)> SpentBySpell =>
        field ??= SylvieManaTracker.SpentBySpellBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>Milliseconds spent at a full mana pool, where regeneration had nowhere to go.</summary>
    public int AtFullMs { get; private set; }

    /// <summary>Milliseconds of mana readings taken, the denominator <see cref="AtFullMs"/> is a share of.</summary>
    public int MeasuredMs { get; private set; }

    /// <summary>Share (0-1) of measured time spent at a full mana pool.</summary>
    public double AtFullShare => MeasuredMs > 0 ? AtFullMs / (double)MeasuredMs : 0;

    /// <summary>The lowest mana reading taken this pull, as a share (0-1) of the pool.</summary>
    public double LowestShare { get; private set; } = 1;

    /// <summary>The mana reading taken when the pull ended, as a share (0-1) of the pool.</summary>
    public double EndingShare => _lastMax > 0 ? _lastAmount / (double)_lastMax : 0;

    /// <summary>Mana spent per second of the pull.</summary>
    public double SpentPerSecond
    {
        get
        {
            var seconds = Math.Max(0, Pull.EndTime - Pull.StartTime) / 1000d;
            return seconds > 0 ? Spent / seconds : 0;
        }
    }

    [On<Event>]
    private void OnAnyEvent(Event e)
    {
        var resources = e switch
        {
            IHasSourceEvent source when Owner.ByPlayer(source) => e.SourceResources,
            IHasTargetEvent target when Owner.ToPlayer(target) => e.TargetResources,
            _ => null,
        };

        if (resources?.Resources is not { Count: > 0 } list) return;

        foreach (var resource in list)
        {
            if (resource.Type != ResourceTypes.Mana || resource.Max <= 0) continue;

            if (_seeded)
            {
                var elapsed = e.Timestamp - _lastSample;
                if (elapsed > 0)
                {
                    MeasuredMs += elapsed;
                    if (_lastAmount >= _lastMax) AtFullMs += elapsed;
                }
            }

            _seeded = true;
            _lastSample = e.Timestamp;
            _lastAmount = resource.Amount;
            _lastMax = resource.Max;
            LowestShare = Math.Min(LowestShare, resource.Amount / (double)resource.Max);
            return;
        }
    }
}

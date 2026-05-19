using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Scores each Spirit of Heroism (ultimate) window against the pre-ult checklist: Skystrider's
/// Supremacy, Voidbringer's Touch, Skystrider's Grace buff, Event Horizon buff, and a Heartseeker
/// Barrage available to fire during the window.
/// </summary>
public sealed partial class PreUltimateChecklistAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    private const int PreUltLookbackMs = 6000;

    private readonly List<int> _supremacyCasts = [];
    private readonly List<int> _voidbringerCasts = [];
    private bool _graceBuffActive;
    private bool _eventHorizonBuffActive;

    private readonly List<UltWindow> _windows = [];

    public IReadOnlyList<UltWindow> Windows => _windows;
    public int Score =>
        _windows.Count == 0
            ? 0
            : (int)Math.Round(_windows.Average(w => w.ChecksPassed * 100.0 / w.TotalChecks));

    public override Type? StatisticsComponentType => typeof(PreUltimateChecklistStatistics);

    [On<CastEvent>(By = Actor.Player, Spell = SpellIds.SkystridersSupremacy)]
    private void OnSupremacy(CastEvent e) => _supremacyCasts.Add(e.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = SpellIds.VoidbringerTouch)]
    private void OnVoidbringer(CastEvent e) => _voidbringerCasts.Add(e.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = SpellIds.SkystridersGraceBuff)]
    private void OnGraceApply(ApplyBuffEvent e) => _graceBuffActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = SpellIds.SkystridersGraceBuff)]
    private void OnGraceRemove(RemoveBuffEvent e) => _graceBuffActive = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = SpellIds.EventHorizonBuff)]
    private void OnEventHorizonApply(ApplyBuffEvent e) => _eventHorizonBuffActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = SpellIds.EventHorizonBuff)]
    private void OnEventHorizonRemove(RemoveBuffEvent e) => _eventHorizonBuffActive = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = SpellIds.SpiritOfHeroismBuff)]
    private void OnUltBuffApply(ApplyBuffEvent e) => RecordWindow(e.Timestamp);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = SpellIds.SpiritOfHeroismBuff)]
    private void OnUltDebuffApply(ApplyDebuffEvent e) => RecordWindow(e.Timestamp);

    private void RecordWindow(int timestamp)
    {
        if (_windows.LastOrDefault().UltTimestamp == timestamp) return;

        var supremacyRecent = _supremacyCasts.Any(t => timestamp - t is >= 0 and <= PreUltLookbackMs);
        var voidbringerRecent = _voidbringerCasts.Any(t => timestamp - t is >= 0 and <= PreUltLookbackMs);
        var barrageAvailable = spellUsable.Value.IsAvailable(SpellIds.HeartseekerBarrage);

        var window = new UltWindow(
            UltTimestamp: timestamp,
            SupremacyCastBefore: supremacyRecent,
            VoidbringerCastBefore: voidbringerRecent,
            SkystridersGraceActive: _graceBuffActive,
            EventHorizonActive: _eventHorizonBuffActive,
            BarrageAvailable: barrageAvailable);

        _windows.Add(window);
    }

    public readonly record struct UltWindow(
        int UltTimestamp,
        bool SupremacyCastBefore,
        bool VoidbringerCastBefore,
        bool SkystridersGraceActive,
        bool EventHorizonActive,
        bool BarrageAvailable)
    {
        public int ChecksPassed =>
            (SupremacyCastBefore ? 1 : 0) +
            (VoidbringerCastBefore ? 1 : 0) +
            (SkystridersGraceActive ? 1 : 0) +
            (EventHorizonActive ? 1 : 0) +
            (BarrageAvailable ? 1 : 0);

        public int TotalChecks => 5;
    }
}

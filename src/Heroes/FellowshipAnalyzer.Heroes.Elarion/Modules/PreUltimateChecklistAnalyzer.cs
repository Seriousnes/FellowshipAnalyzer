using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

using Items = FellowshipAnalyzer.Core.Common.Spells.Items;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Scores each Spirit of Heroism (ultimate) window against the pre-ult checklist: Skystrider's
/// Supremacy, Voidbringer's Touch, Skystrider's Grace buff, Event Horizon buff, and a Heartseeker
/// Barrage available to fire during the window. Ultimate setup is shape-agnostic, so this runs on
/// every pull shape.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Uses<SpellUsable>]
public sealed partial class PreUltimateChecklistAnalyzer : Analyzer
{
    private const int PreUltLookbackMs = 6000;

    private readonly List<int> _supremacyCasts = [];
    private readonly List<int> _voidbringerCasts = [];
    private bool _graceBuffActive;
    private bool _eventHorizonBuffActive;

    private readonly List<UltWindow> _windows = [];

    public IReadOnlyList<UltWindow> Windows => _windows;

    /// <summary>Average share (0-100) of the five pre-conditions met across all ult windows.</summary>
    public int Score { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SkystridersSupremacy))]
    private void OnSupremacy(CastEvent castEvent) => _supremacyCasts.Add(castEvent.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Items.VoidbringerTouch))]
    private void OnVoidbringer(CastEvent castEvent) => _voidbringerCasts.Add(castEvent.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersGraceBuff))]
    private void OnGraceApply(ApplyBuffEvent _) => _graceBuffActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersGraceBuff))]
    private void OnGraceRemove(RemoveBuffEvent _) => _graceBuffActive = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnEventHorizonApply(ApplyBuffEvent _) => _eventHorizonBuffActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnEventHorizonRemove(RemoveBuffEvent _) => _eventHorizonBuffActive = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SpiritOfHeroism))]
    private void OnUltBuffApply(ApplyBuffEvent applyBuffEvent) => RecordWindow(applyBuffEvent.Timestamp);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SpiritOfHeroism))]
    private void OnUltDebuffApply(ApplyDebuffEvent applyDebuffEvent) => RecordWindow(applyDebuffEvent.Timestamp);

    [On<PullEndEvent>]
    public void OnPullEnd(PullEndEvent _)
    {
        Score = _windows.Count == 0
            ? 0
            : (int)Math.Round(_windows.Average(w => w.ChecksPassed * 100.0 / w.TotalChecks));
    }

    private void RecordWindow(int timestamp)
    {
        if (_windows.Count > 0 && _windows[^1].UltTimestamp == timestamp) return;

        var supremacyRecent = _supremacyCasts.Any(t => timestamp - t is >= 0 and <= PreUltLookbackMs);
        var voidbringerRecent = _voidbringerCasts.Any(t => timestamp - t is >= 0 and <= PreUltLookbackMs);
        var barrageAvailable = SpellUsable.IsAvailable(Spells.HeartseekerBarrage.FSLID);

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

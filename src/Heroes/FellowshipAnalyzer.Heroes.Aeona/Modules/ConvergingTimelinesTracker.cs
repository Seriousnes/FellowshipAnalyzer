using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Converging Timelines on the player: the Cooldown Acceleration each application grants, added to
/// <see cref="StatTracker"/>'s pool while the buff runs so <see cref="SpellUsable"/> recharges
/// Entropy's Claim and every other cooldown at the rate the game did.
/// </summary>
/// <remarks>
/// The magnitude depends on the cast that applied the buff: Oblivion grants
/// <see cref="AeonaBuild.ConvergingTimelinesOnOblivion"/>, a cleanse grants
/// <see cref="AeonaBuild.ConvergingTimelinesOnCleanse"/>. A refresh inside a running window replaces the
/// modifier with the refreshing cast's magnitude and extends the window.
/// </remarks>
[Dependency<StatTracker>]
[Dependency<AeonaBuild>]
[Before<SpellUsable>]
public sealed partial class ConvergingTimelinesTracker : Analyzer
{
    private readonly List<AuraWindow> _windows = [];

    private CooldownModifier? _active;
    private int? _openedAt;
    private bool _lastCastWasOblivion;

    /// <summary>Applications and refreshes of Converging Timelines on the player.</summary>
    public int Applications { get; private set; }

    /// <summary>Applications granted by an Oblivion cast.</summary>
    public int OblivionApplications { get; private set; }

    /// <summary>Applications granted by an Amend Fate or Restore Continuity cast.</summary>
    public int CleanseApplications { get; private set; }

    /// <summary>Every window the buff was active on the player, in the order they opened.</summary>
    public IReadOnlyList<AuraWindow> Windows =>
        _openedAt is { } start ? [.. _windows, new AuraWindow(start, Math.Max(start, Owner.DungeonEndTime))] : _windows;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnOblivionCast(CastEvent e) => _lastCastWasOblivion = true;

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseCast(CastEvent e) => _lastCastWasOblivion = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelines))]
    private void OnApplied(ApplyBuffEvent e) => Grant(e, e.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelines))]
    private void OnRefreshed(RefreshBuffEvent e) => Grant(e, e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelines))]
    private void OnRemoved(RemoveBuffEvent e)
    {
        Revoke(e, e.Timestamp);

        if (_openedAt is not { } start) return;

        _windows.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
        _openedAt = null;
    }

    private void Grant(Event trigger, int timestamp)
    {
        Applications++;
        if (_lastCastWasOblivion) OblivionApplications++;
        else CleanseApplications++;

        Revoke(trigger, timestamp);

        var magnitude = _lastCastWasOblivion ? AeonaBuild.ConvergingTimelinesOnOblivion : AeonaBuild.ConvergingTimelinesOnCleanse;
        _active = new CooldownModifier(magnitude);
        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, _active, trigger, timestamp);
        _openedAt ??= timestamp;
    }

    private void Revoke(Event trigger, int timestamp)
    {
        if (_active is not { } active) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, active, trigger, timestamp);
        _active = null;
    }
}

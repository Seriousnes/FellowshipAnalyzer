using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Uchronia's buff windows on the player, so <see cref="FreeCastTracker"/> can attribute a free cast
/// to the proc that made it free and a segment can count the procs a pull offered.
/// </summary>
/// <remarks>
/// A window opens on the first application and closes on the removal that consumes it; an
/// application arriving while a window is open extends it rather than opening a second. A window
/// still open when the dungeon ends closes at <see cref="CombatLogParser.DungeonEndTime"/> and
/// counts in <see cref="Procs"/> like any other. Window membership includes the endpoints, so a free
/// cast logged in the same millisecond as the removal that consumed the buff falls inside it.
/// </remarks>
[RequiresTalent(AeonaTalents.Uchronia)]
public sealed partial class UchroniaTracker : Analyzer
{
    private readonly List<AuraWindow> _windows = [];

    private int? _openedAt;

    /// <summary>Every Uchronia window on the player, in the order they opened.</summary>
    public IReadOnlyList<AuraWindow> Windows =>
        _openedAt is { } start ? [.. _windows, CloseAtDungeonEnd(start)] : _windows;

    /// <summary>Uchronia windows opened.</summary>
    public int Procs => _windows.Count + (_openedAt is null ? 0 : 1);

    /// <summary>Whether a Uchronia window covered <paramref name="timestamp"/>, endpoints included.</summary>
    public bool IsActive(int timestamp)
    {
        if (_openedAt is { } start && timestamp >= start && timestamp <= CloseAtDungeonEnd(start).End) return true;

        foreach (var window in _windows)
        {
            if (timestamp >= window.Start && timestamp <= window.End) return true;
        }

        return false;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Uchronia))]
    private void OnUchroniaApplied(ApplyBuffEvent e) => _openedAt ??= e.Timestamp;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Uchronia))]
    private void OnUchroniaRefreshed(RefreshBuffEvent e) => _openedAt ??= e.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Uchronia))]
    private void OnUchroniaRemoved(RemoveBuffEvent e)
    {
        if (_openedAt is not { } start) return;

        _windows.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
        _openedAt = null;
    }

    private AuraWindow CloseAtDungeonEnd(int start) => new(start, Math.Max(start, Owner.DungeonEndTime));
}

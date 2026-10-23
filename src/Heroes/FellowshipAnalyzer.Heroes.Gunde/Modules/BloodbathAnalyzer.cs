using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

using GundeTalents = FellowshipAnalyzer.Core.Common.Spells.GundeTalents;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Bloodbath: each Grim Carve grants Bloodbath for 3 seconds, and Reign in Blood has +200% Cooldown
/// Acceleration while it lasts. The logged Bloodbath <see cref="ApplyBuffEvent"/> on the player opens a window
/// and adds the term to the pool <see cref="StatTracker"/> tracks. A <see cref="RefreshBuffEvent"/> extends the
/// open window, and the logged <see cref="RemoveBuffEvent"/> closes it and removes the term.
/// </summary>
[RequiresTalent(GundeTalents.Bloodbath)]
[Dependency<StatTracker>]
public sealed partial class BloodbathAnalyzer : Analyzer
{
    /// <summary>Reign in Blood's added Cooldown Acceleration while Bloodbath lasts, as a fraction.</summary>
    public const double AddedAcceleration = 2.0;

    /// <summary>Bloodbath's duration, in milliseconds. An open window ends this long after its last apply or refresh until the removal closes it.</summary>
    public const int DurationMs = 3000;

    private static readonly CooldownModifier ReignInBloodAcceleration =
        new(AddedAcceleration, new Core.Common.Spells.Spell[] { Spells.ReignInBlood });

    private readonly List<BloodbathWindow> _windows = [];
    private bool _open;

    /// <summary>Every Bloodbath window for the selected player.</summary>
    public IReadOnlyList<BloodbathWindow> Windows => _windows;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Bloodbath))]
    private void OnApply(ApplyBuffEvent e) => OpenOrExtend(e);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Bloodbath))]
    private void OnRefresh(RefreshBuffEvent e) => OpenOrExtend(e);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Bloodbath))]
    private void OnRemove(RemoveBuffEvent e)
    {
        if (!_open) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, ReignInBloodAcceleration, e);
        _windows[^1] = _windows[^1] with { EndTimestamp = e.Timestamp };
        _open = false;
    }

    private void OpenOrExtend(BuffEvent e)
    {
        if (_open)
        {
            _windows[^1] = _windows[^1] with { EndTimestamp = e.Timestamp + DurationMs };
            return;
        }

        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, ReignInBloodAcceleration, e);
        _windows.Add(new BloodbathWindow(e.Timestamp, e.Timestamp + DurationMs));
        _open = true;
    }
}

/// <summary>One Bloodbath window: the interval Reign in Blood's Cooldown Acceleration was raised.</summary>
public sealed record BloodbathWindow(int BeginTimestamp, int EndTimestamp);

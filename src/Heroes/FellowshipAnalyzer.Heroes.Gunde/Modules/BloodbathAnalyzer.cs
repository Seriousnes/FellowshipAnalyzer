using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using GundeTalents = FellowshipAnalyzer.Core.Common.Spells.GundeTalents;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Bloodbath: each Grim Carve grants Bloodbath for 3 seconds, and Reign in Blood has Cooldown
/// Acceleration while it lasts. The logged Bloodbath <see cref="ApplyBuffEvent"/> on the player opens a window
/// and adds the term to the pool <see cref="StatTracker"/> tracks. A <see cref="RefreshBuffEvent"/> extends the
/// open window, and the logged <see cref="RemoveBuffEvent"/> closes it and removes the term.
/// </summary>
[RequiresTalent(GundeTalents.Bloodbath)]
[Dependency<StatTracker>]
public sealed partial class BloodbathAnalyzer : Analyzer
{
    private static readonly CooldownAccelerationChange Change =
        EffectData.CooldownAccelerationsOf(Spells.Bloodbath.Id) is [var change, ..]
            ? change
            : throw new InvalidOperationException(
                $"The export gives effect {Spells.Bloodbath.Id} no Cooldown Acceleration change.");

    private static readonly int DefaultDurationMs =
        (int)((EffectData.DurationOf(Spells.Bloodbath.Id)
            ?? throw new InvalidOperationException($"The export gives effect {Spells.Bloodbath.Id} no duration.")) * 1000);

    private static readonly CooldownModifier ReignInBloodAcceleration =
        new(Change.Value, new Core.Common.Spells.Spell[] { new() { Id = Change.AbilityId } });

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
            _windows[^1] = _windows[^1] with { EndTimestamp = e.ExpectedEnd ?? e.Timestamp + DefaultDurationMs };
            return;
        }

        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, ReignInBloodAcceleration, e);
        _windows.Add(new BloodbathWindow(e.Timestamp, e.ExpectedEnd ?? e.Timestamp + DefaultDurationMs));
        _open = true;
    }
}

/// <summary>One Bloodbath window: the interval Reign in Blood's Cooldown Acceleration was raised.</summary>
public sealed record BloodbathWindow(int BeginTimestamp, int EndTimestamp);

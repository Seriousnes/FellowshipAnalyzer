using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Cooldown Acceleration granted by Converging Timelines on the player, added to <see cref="StatTracker"/>'s pool while the buff is active.
/// </summary>
/// <remarks>
/// Converging Timelines is two effects. Effect 3266 takes its magnitude from the cast that applied it: Oblivion grants
/// <see cref="AeonaBuild.ConvergingTimelinesOnOblivion"/>, a cleanse grants
/// <see cref="AeonaBuild.ConvergingTimelinesOnCleanse"/>. Effect 2820 is applied by Lonesome Song's ability 2018 and
/// multiplies Cooldown Recovery by 3, a term of <c>3 - 1</c> in the additive pool; it counts as an Oblivion application.
/// Each effect holds its own modifier, so the two terms add while both are active. A refresh inside a running window
/// replaces that effect's modifier and extends the window.
/// </remarks>
[Dependency<StatTracker>]
[Dependency<AeonaBuild>]
[Before<SpellUsable>]
public sealed partial class ConvergingTimelinesTracker : Analyzer
{
    private const double LonesomeSongAcceleration = 3 - 1;

    private readonly List<AuraWindow> _windows = [];
    private readonly Aura _convergingTimelines = new();
    private readonly Aura _lonesomeSong = new();

    private bool _lastCastWasOblivion;

    /// <summary>Applications and refreshes of Converging Timelines on the player.</summary>
    public int Applications { get; private set; }

    /// <summary>Applications granted by an Oblivion cast.</summary>
    public int OblivionApplications { get; private set; }

    /// <summary>Applications granted by an Amend Fate or Restore Continuity cast.</summary>
    public int CleanseApplications { get; private set; }

    /// <summary>Every window either Converging Timelines effect was active on the player, in the order they opened.</summary>
    public IReadOnlyList<AuraWindow> Windows =>
    [
        .. new[] { _convergingTimelines.OpenedAt, _lonesomeSong.OpenedAt }
            .OfType<int>()
            .Select(start => new AuraWindow(start, Math.Max(start, Owner.DungeonEndTime)))
            .Concat(_windows)
            .OrderBy(window => window.Start),
    ];

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnOblivionCast(CastEvent e) => _lastCastWasOblivion = true;

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseCast(CastEvent e) => _lastCastWasOblivion = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelines))]
    private void OnApplied(ApplyBuffEvent e) => GrantConvergingTimelines(e);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelines))]
    private void OnRefreshed(RefreshBuffEvent e) => GrantConvergingTimelines(e);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelines))]
    private void OnRemoved(RemoveBuffEvent e) => Close(_convergingTimelines, e);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelinesLonesomeSong))]
    private void OnLonesomeSongApplied(ApplyBuffEvent e) => GrantLonesomeSong(e);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelinesLonesomeSong))]
    private void OnLonesomeSongRefreshed(RefreshBuffEvent e) => GrantLonesomeSong(e);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ConvergingTimelinesLonesomeSong))]
    private void OnLonesomeSongRemoved(RemoveBuffEvent e) => Close(_lonesomeSong, e);

    private void GrantConvergingTimelines(Event trigger)
    {
        Applications++;
        if (_lastCastWasOblivion) OblivionApplications++;
        else CleanseApplications++;

        var magnitude = _lastCastWasOblivion ? AeonaBuild.ConvergingTimelinesOnOblivion : AeonaBuild.ConvergingTimelinesOnCleanse;
        Grant(_convergingTimelines, magnitude, trigger);
    }

    private void GrantLonesomeSong(Event trigger)
    {
        Applications++;
        OblivionApplications++;
        Grant(_lonesomeSong, LonesomeSongAcceleration, trigger);
    }

    private void Grant(Aura aura, double magnitude, Event trigger)
    {
        Revoke(aura, trigger);

        aura.Modifier = new CooldownModifier(magnitude);
        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, aura.Modifier, trigger, trigger.Timestamp);
        aura.OpenedAt ??= trigger.Timestamp;
    }

    private void Close(Aura aura, Event trigger)
    {
        Revoke(aura, trigger);

        if (aura.OpenedAt is not { } start) return;

        _windows.Add(new AuraWindow(start, Math.Max(start, trigger.Timestamp)));
        aura.OpenedAt = null;
    }

    private void Revoke(Aura aura, Event trigger)
    {
        if (aura.Modifier is not { } active) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, active, trigger, trigger.Timestamp);
        aura.Modifier = null;
    }

    private sealed class Aura
    {
        public CooldownModifier? Modifier { get; set; }

        public int? OpenedAt { get; set; }
    }
}

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Cooldown Recovery granted by Converging Timelines on the player, joined to <see cref="StatTracker"/>'s Cooldown Recovery pool while the buff is active.
/// </summary>
/// <remarks>
/// Converging Timelines is two effects. Effect 3266 multiplies Cooldown Recovery by 2. Effect 2820 is applied by
/// Lonesome Song's ability 2018, multiplies Cooldown Recovery by 3 and counts as an Oblivion application. Each
/// effect joins the pool with the value and operation of its export modifier, so the effects reach only the
/// cooldowns that scale off Cooldown Recovery.
/// One Oblivion cast counts once in <see cref="Applications"/> and <see cref="OblivionApplications"/>, even when it
/// grants both effects. A refresh inside a running window replaces that effect's modifier and extends the window.
/// </remarks>
[Dependency<StatTracker>]
[Dependency<AeonaBuild>]
[Before<SpellUsable>]
public sealed partial class ConvergingTimelinesTracker : Analyzer
{
    private static readonly CooldownModifier ConvergingTimelinesRecovery =
        CooldownRecoveryModifiers.Of(Spells.ConvergingTimelines);

    private static readonly CooldownModifier LonesomeSongRecovery =
        CooldownRecoveryModifiers.Of(Spells.ConvergingTimelinesLonesomeSong);

    private readonly List<AuraWindow> _windows = [];
    private readonly Aura _convergingTimelines = new();
    private readonly Aura _lonesomeSong = new();

    private bool _lastCastWasOblivion;
    private bool _castCounted;

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
    private void OnOblivionCast(CastEvent e) => BeginCast(oblivion: true);

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseCast(CastEvent e) => BeginCast(oblivion: false);

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

    private void BeginCast(bool oblivion)
    {
        _lastCastWasOblivion = oblivion;
        _castCounted = false;
    }

    private void GrantConvergingTimelines(Event trigger)
    {
        if (_lastCastWasOblivion) CountOblivion();
        else CountCleanse();

        Grant(_convergingTimelines, ConvergingTimelinesRecovery, trigger);
    }

    private void GrantLonesomeSong(Event trigger)
    {
        CountOblivion();
        Grant(_lonesomeSong, LonesomeSongRecovery, trigger);
    }

    private void CountOblivion()
    {
        if (_lastCastWasOblivion && _castCounted) return;

        Applications++;
        OblivionApplications++;
        _castCounted = _lastCastWasOblivion;
    }

    private void CountCleanse()
    {
        Applications++;
        CleanseApplications++;
    }

    private void Grant(Aura aura, CooldownModifier modifier, Event trigger)
    {
        Revoke(aura, trigger);

        aura.Modifier = modifier;
        StatTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, modifier, trigger, trigger.Timestamp);
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

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownRecovery, active, trigger, trigger.Timestamp);
        aura.Modifier = null;
    }

    private sealed class Aura
    {
        public CooldownModifier? Modifier { get; set; }

        public int? OpenedAt { get; set; }
    }
}

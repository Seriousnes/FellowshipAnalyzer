using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface Unfolding Doom is measured on.</summary>
public interface IUnfoldingDoomAnalyzer : IAnalyzerSurface;

/// <summary>
/// Unfolding Doom reapplied to an enemy that still carried the debuff.
/// </summary>
/// <param name="Unit">The enemy the debuff was reapplied to.</param>
/// <param name="Timestamp">When the reapplication landed.</param>
/// <param name="OverlappedMs">
/// The duration the reapplication discarded: the remaining time on the previous application,
/// counted from its own start plus <see cref="UnfoldingDoomAnalyzer.DebuffDurationMs"/>. Zero when
/// the previous application had already run its full duration without the log reporting its removal.
/// </param>
public sealed record UnfoldingDoomReapplication(UnitKey Unit, int Timestamp, int OverlappedMs);

/// <summary>
/// One application of Unfolding Doom: the stretch it ran on one enemy, with reapplications merged into
/// it, and what it was worth.
/// </summary>
/// <param name="Unit">The debuffed enemy.</param>
/// <param name="Start">When the debuff landed.</param>
/// <param name="End">When it left.</param>
/// <param name="Damage">The damage the player dealt to that enemy inside the stretch, before absorbs.</param>
/// <param name="DamageGained">The share of <paramref name="Damage"/> the debuff's increase accounts for.</param>
/// <param name="DelayAfterReadyMs">
/// How long Unfolding Doom sat available with no enemy debuffed before this application closed that
/// stretch. Zero when the debuff landed while another enemy still carried it, or the moment the cast
/// became available.
/// </param>
public sealed record UnfoldingDoomApplication(
    UnitKey Unit,
    int Start,
    int End,
    long Damage,
    long DamageGained,
    int DelayAfterReadyMs)
{
    /// <summary>How long this application ran, in milliseconds.</summary>
    public int ActiveMs => End - Start;
}

/// <summary>
/// Measures Unfolding Doom over one pull: the debuff's union uptime across every enemy it was applied
/// to, the damage its increase accounts for, and the time the cast was available with no enemy debuffed.
/// </summary>
/// <remarks>
/// <para>
/// Uptime is a union: a millisecond counts once however many enemies carry the debuff, which is what
/// the damage increase is worth to the player. Each enemy's own stretches are <see cref="Applications"/>.
/// </para>
/// <para>
/// The debuff drives no events of its own on the enemy - it emits no ticks - so windows are built
/// from applications and removals alone. A window the log never closes collapses to zero length and
/// drops out of the measurement rather than running to the end of the pull.
/// </para>
/// <para>
/// Availability is seeded from <see cref="SpellUsable"/> at the pull start and then follows the
/// <see cref="UpdateSpellUsableEvent"/> stream, so a cooldown that began in an earlier pull is
/// carried in rather than treated as ready.
/// </para>
/// <para>
/// <see cref="Reapplications"/> and <see cref="OverlappedMs"/> are kept for the cross-ability guide,
/// which is where the doc puts the duration a reapplication discards.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class UnfoldingDoomAnalyzer : AllTargetUptimeAnalyzer, IUnfoldingDoomAnalyzer
{
    /// <summary>
    /// How long one application of the debuff lasts, in milliseconds. Codex effect 2624 reports a
    /// duration of 20 seconds, matching the duration the log's own application events carry.
    /// </summary>
    public const int DebuffDurationMs = 20_000;

    /// <summary>
    /// The increase the debuff applies to the player's damage against the debuffed enemy. Codex
    /// ability 1890 states "increasing all damage you deal to them by 20% for 20 seconds", and codex
    /// effect 2624 states "Taking 20% more damage from you".
    /// </summary>
    public const double DamageIncrease = 0.20;

    private readonly Dictionary<UnitKey, int> _openStarts = [];
    private readonly List<DamageEvent> _playerDamage = [];
    private readonly List<UnfoldingDoomReapplication> _reapplications = [];
    private readonly List<AvailabilityChange> _availability = [];

    private Computed Result => field ??= Compute();

    /// <summary>Whether the player took Hastening Doom, which grants Haste while the debuff is active.</summary>
    public bool HasteningDoomTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.HasteningDoom);

    /// <summary>Unfolding Doom casts during the pull.</summary>
    public int Casts { get; private set; }

    /// <summary>Milliseconds with the debuff active on at least one enemy, counting a millisecond once.</summary>
    public int ActiveMs => Result.ActiveMs;

    /// <summary>Share of the pull (0-1) with the debuff active on at least one enemy.</summary>
    public double Uptime => Result.Uptime;

    /// <summary>Every stretch the debuff ran on one enemy, in the order they landed.</summary>
    public IReadOnlyList<UnfoldingDoomApplication> Applications => Result.Applications;

    /// <summary>
    /// The damage the player dealt to debuffed enemies while the debuff was active, before absorbs.
    /// This is the figure <see cref="DamageGained"/> is taken from.
    /// </summary>
    public long DamageWhileActive => Result.Applications.Sum(application => application.Damage);

    /// <summary>
    /// The share of <see cref="DamageWhileActive"/> the debuff's increase accounts for, taken through
    /// <see cref="CombatMath.CalculateEffectiveDamage"/>.
    /// </summary>
    public long DamageGained => Result.Applications.Sum(application => application.DamageGained);

    /// <summary>Milliseconds of the pull with Unfolding Doom available to cast.</summary>
    public int AvailableMs => Result.AvailableMs;

    /// <summary>Every stretch of the pull with Unfolding Doom available and no enemy carrying the debuff.</summary>
    public IReadOnlyList<AuraWindow> IdleWindows => Result.IdleWindows;

    /// <summary>Milliseconds of the pull with Unfolding Doom available and no enemy carrying the debuff.</summary>
    public int IdleAvailableMs => Result.IdleWindows.Sum(window => window.Duration);

    /// <summary>Share (0-1) of the pull spent available with no enemy debuffed.</summary>
    public double IdleAvailableShare => Pull.Duration > 0 ? IdleAvailableMs / (double)Pull.Duration : 0;

    /// <summary>Every reapplication onto an enemy that still carried the debuff, in the order they landed.</summary>
    public IReadOnlyList<UnfoldingDoomReapplication> Reapplications => _reapplications;

    /// <summary>Remaining debuff duration discarded across every <see cref="Reapplications"/> entry.</summary>
    public int OverlappedMs => _reapplications.Sum(entry => entry.OverlappedMs);

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e) =>
        _availability.Add(new AvailabilityChange(
            e.StartTime,
            SpellUsable.CooldownRemaining(Spells.UnfoldingDoom.FSLID, e.StartTime) <= 0));

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoom))]
    private void OnUsableChanged(UpdateSpellUsableEvent e) =>
        _availability.Add(new AvailabilityChange(e.Timestamp, e.IsAvailable));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoom))]
    private void OnCast() => Casts++;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoomDebuff))]
    private void OnApplied(ApplyDebuffEvent e) => RecordApplication(e);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoomDebuff))]
    private void OnReapplied(RefreshDebuffEvent e) => RecordApplication(e);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoomDebuff))]
    private void OnRemoved(RemoveDebuffEvent e)
    {
        _openStarts.Remove(AuraWindowLedger.KeyOf(e));
        CloseWindow(e, e.Timestamp);
    }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnPlayerDamage(DamageEvent e) => _playerDamage.Add(e);

    private void RecordApplication(BuffEvent e)
    {
        var unit = AuraWindowLedger.KeyOf(e);

        if (_openStarts.TryGetValue(unit, out var previousStart))
        {
            _reapplications.Add(new UnfoldingDoomReapplication(
                unit,
                e.Timestamp,
                Math.Max(0, previousStart + DebuffDurationMs - e.Timestamp)));
        }

        _openStarts[unit] = e.Timestamp;
        OpenWindow(e, e.Timestamp);
    }

    private Computed Compute()
    {
        var windowsByTarget = TargetUptimes.ToDictionary(target => target.Unit, target => target.Windows);
        var debuffed = Merge([.. windowsByTarget.Values.SelectMany(windows => windows)]);
        var activeMs = debuffed.Sum(window => window.Duration);
        var duration = Pull.EndTime - Pull.StartTime;

        var available = AvailableWindows();
        var idle = Outside(available, debuffed);

        var applications = new List<UnfoldingDoomApplication>();
        foreach (var (unit, windows) in windowsByTarget)
        {
            foreach (var window in windows)
            {
                if (window.Duration <= 0) continue;

                var damage = 0L;
                var gain = 0L;

                foreach (var e in _playerDamage)
                {
                    if (AuraWindowLedger.KeyOf(e) != unit) continue;
                    if (e.Timestamp < window.Start || e.Timestamp > window.End) continue;

                    damage += e.Amount + (e.Absorbed ?? 0);
                    gain += CombatMath.CalculateEffectiveDamage(e, DamageIncrease);
                }

                applications.Add(new UnfoldingDoomApplication(
                    unit,
                    window.Start,
                    window.End,
                    damage,
                    gain,
                    DelayBefore(idle, window.Start)));
            }
        }

        applications.Sort((left, right) => left.Start.CompareTo(right.Start));

        return new Computed(
            activeMs,
            duration > 0 ? Math.Min(1d, activeMs / (double)duration) : 0d,
            applications,
            available.Sum(window => window.Duration),
            idle);
    }

    private static int DelayBefore(List<AuraWindow> idle, int start)
    {
        foreach (var window in idle)
        {
            if (window.End == start) return window.Duration;
        }

        return 0;
    }

    private List<AuraWindow> AvailableWindows()
    {
        var start = Pull.StartTime;
        var end = Pull.EndTime;
        var windows = new List<AuraWindow>();
        var open = false;
        var openedAt = start;

        foreach (var change in _availability)
        {
            if (change.Available == open) continue;

            var at = Math.Clamp(change.Timestamp, start, end);
            open = change.Available;

            if (open) openedAt = at;
            else if (at > openedAt) windows.Add(new AuraWindow(openedAt, at));
        }

        if (open && end > openedAt) windows.Add(new AuraWindow(openedAt, end));

        return windows;
    }

    private static List<AuraWindow> Merge(List<AuraWindow> windows)
    {
        if (windows.Count == 0) return [];

        var ordered = windows.OrderBy(window => window.Start).ToList();
        var merged = new List<AuraWindow>();
        var current = ordered[0];

        foreach (var window in ordered.Skip(1))
        {
            if (window.Start > current.End)
            {
                merged.Add(current);
                current = window;
                continue;
            }

            current = current with { End = Math.Max(current.End, window.End) };
        }

        merged.Add(current);
        return merged;
    }

    /// <summary>
    /// The stretches of <paramref name="windows"/> that no block of <paramref name="subtract"/> covers,
    /// which is Unfolding Doom available with no enemy debuffed.
    /// </summary>
    private static List<AuraWindow> Outside(List<AuraWindow> windows, List<AuraWindow> subtract)
    {
        var outside = new List<AuraWindow>();

        foreach (var window in windows)
        {
            var cursor = window.Start;

            foreach (var block in subtract)
            {
                if (block.End <= cursor) continue;
                if (block.Start >= window.End) break;
                if (block.Start > cursor) outside.Add(new AuraWindow(cursor, block.Start));

                cursor = Math.Max(cursor, block.End);
                if (cursor >= window.End) break;
            }

            if (cursor < window.End) outside.Add(new AuraWindow(cursor, window.End));
        }

        return outside;
    }

    private readonly record struct AvailabilityChange(int Timestamp, bool Available);

    private sealed record Computed(
        int ActiveMs,
        double Uptime,
        List<UnfoldingDoomApplication> Applications,
        int AvailableMs,
        List<AuraWindow> IdleWindows);
}

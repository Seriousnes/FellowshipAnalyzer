using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface Unfolding Doom is measured on.</summary>
public interface IUnfoldingDoomAnalyzer : IAnalyzerSurface;

/// <summary>Where the enemy one application went on sat in its window's damage order.</summary>
public enum UnfoldingDoomTargetOutcome
{
    /// <summary>The enemy that took the most of the player's damage over the window.</summary>
    Priority,

    /// <summary>An enemy further down the window's damage order.</summary>
    Alternate,

    /// <summary>The only enemy in the window's damage order.</summary>
    SoleTarget,
}

/// <summary>
/// Unfolding Doom reapplied to an enemy the debuff was already active on.
/// </summary>
/// <param name="Unit">The enemy the debuff was reapplied to.</param>
/// <param name="Timestamp">When the debuff was reapplied.</param>
/// <param name="OverlappedMs">
/// The remaining duration on the previous application, discarded by the reapplication. Zero once the
/// previous application has run its full duration.
/// </param>
public sealed record UnfoldingDoomReapplication(UnitKey Unit, int Timestamp, int OverlappedMs);

/// <summary>
/// One application of Unfolding Doom: the stretch one cast held the debuff on one enemy, ending at the
/// reapplication that took it over or at the removal.
/// </summary>
/// <param name="Unit">The debuffed enemy.</param>
/// <param name="Start">When the debuff was applied.</param>
/// <param name="End">When the next application took the debuff over, or when it was removed.</param>
/// <param name="Damage">The damage the player dealt to that enemy inside the stretch, before absorbs.</param>
/// <param name="DamageGained">The share of <paramref name="Damage"/> the debuff's increase accounts for.</param>
/// <param name="DelayAfterReadyMs">
/// How long Unfolding Doom was available with no enemy debuffed before this application closed that
/// stretch. Zero when the debuff was already active when this application opened, or at the moment the
/// cast became available.
/// </param>
/// <param name="Outcome">Where <paramref name="Unit"/> sat in the window's damage order.</param>
/// <param name="Rank">Enemies that took more of the player's damage over the window than <paramref name="Unit"/> did.</param>
/// <param name="Candidates">Enemies in the window's damage order, counting <paramref name="Unit"/>.</param>
/// <param name="WindowDamage">The damage the player dealt to <paramref name="Unit"/> over the window, before absorbs.</param>
/// <param name="BestWindowDamage">The most damage the player dealt to any one enemy over the window, before absorbs.</param>
/// <param name="BestUnit">The enemy that took <paramref name="BestWindowDamage"/>.</param>
/// <param name="DiedAfterMs">Milliseconds from this application to the target's death, or <c>null</c> when it outlived the window.</param>
public sealed record UnfoldingDoomApplication(
    UnitKey Unit,
    int Start,
    int End,
    long Damage,
    long DamageGained,
    int DelayAfterReadyMs,
    UnfoldingDoomTargetOutcome Outcome,
    int Rank,
    int Candidates,
    long WindowDamage,
    long BestWindowDamage,
    UnitKey BestUnit,
    int? DiedAfterMs)
{
    /// <summary>How long this application ran, in milliseconds.</summary>
    public int ActiveMs => End - Start;

    /// <summary>Whether the cast went on the enemy that took the most of the player's damage over the window.</summary>
    public bool OnPriority => Rank == 0;

    /// <summary>Whether the window offered more than one enemy.</summary>
    public bool Rated => Candidates > 1;

    /// <summary>Share (0-1) of <see cref="BestWindowDamage"/> that <see cref="WindowDamage"/> reaches.</summary>
    public double PriorityShare => BestWindowDamage > 0 ? WindowDamage / (double)BestWindowDamage : 1d;
}

/// <summary>
/// Measures Unfolding Doom over one pull: the debuff's union uptime across every enemy it was applied
/// to, the damage its increase accounts for, the enemy each cast went on, and the time the cast was
/// available with no enemy debuffed.
/// </summary>
/// <remarks>
/// <para>
/// Uptime is a union: a millisecond counts once however many enemies the debuff is active on.
/// <see cref="Applications"/> divides that union by cast, so a recast onto an enemy the debuff is
/// already active on closes the earlier application at the reapplication and opens its own. The
/// applications on one enemy therefore partition that enemy's active time, and
/// <see cref="DamageWhileActive"/> counts each of the player's hits once.
/// </para>
/// <para>
/// An application's target is ranked over the <see cref="DebuffDurationMs"/> that follow the cast,
/// clipped to the pull: the priority target is the enemy the player put the most damage into over
/// those seconds, so an enemy that dies early ranks below one that survives to take the increase.
/// </para>
/// <para>
/// An application with no removal collapses to zero length and drops out of the measurement rather
/// than running to the end of the pull.
/// </para>
/// <para>
/// Availability is seeded from <see cref="SpellUsable"/> at the pull start and then follows the
/// <see cref="UpdateSpellUsableEvent"/> stream, so a cooldown that began in an earlier pull continues
/// rather than being treated as ready.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class UnfoldingDoomAnalyzer : AllTargetUptimeAnalyzer, IUnfoldingDoomAnalyzer
{
    /// <summary>How long one application of the debuff lasts, in milliseconds.</summary>
    public const int DebuffDurationMs = 20_000;

    /// <summary>
    /// The increase the debuff applies to the player's damage against the debuffed enemy.
    /// </summary>
    public const double DamageIncrease = 0.20;

    private readonly Dictionary<UnitKey, int> _openStarts = [];
    private readonly Dictionary<UnitKey, int> _deaths = [];
    private readonly List<DamageEvent> _playerDamage = [];
    private readonly List<UnfoldingDoomReapplication> _reapplications = [];
    private readonly List<AvailabilityChange> _availability = [];

    private Computed Result => field ??= Compute();

    /// <summary>Whether the player took Hastening Doom.</summary>
    public bool HasteningDoomTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.HasteningDoom);

    /// <summary>Unfolding Doom casts during the pull.</summary>
    public int Casts { get; private set; }

    /// <summary>Milliseconds with the debuff active on at least one enemy, counting a millisecond once.</summary>
    public int ActiveMs => Result.ActiveMs;

    /// <summary>Share of the pull (0-1) with the debuff active on at least one enemy.</summary>
    public double Uptime => Result.Uptime;

    /// <summary>Every stretch one cast held the debuff on one enemy, in the order they were applied.</summary>
    public IReadOnlyList<UnfoldingDoomApplication> Applications => Result.Applications;

    /// <summary>The damage the player dealt to debuffed enemies, before absorbs.</summary>
    public long DamageWhileActive => Result.Applications.Sum(application => application.Damage);

    /// <summary>
    /// The share of <see cref="DamageWhileActive"/> the debuff's increase accounts for.
    /// </summary>
    public long DamageGained => Result.Applications.Sum(application => application.DamageGained);

    /// <summary>The share of <see cref="DamageGained"/> from <see cref="UnfoldingDoomTargetOutcome.Priority"/> applications.</summary>
    public long PriorityDamageGained => DamageGainedOn(UnfoldingDoomTargetOutcome.Priority);

    /// <summary>The share of <see cref="DamageGained"/> from <see cref="UnfoldingDoomTargetOutcome.Alternate"/> applications.</summary>
    public long AlternateDamageGained => DamageGainedOn(UnfoldingDoomTargetOutcome.Alternate);

    /// <summary>The share of <see cref="DamageGained"/> from <see cref="UnfoldingDoomTargetOutcome.SoleTarget"/> applications.</summary>
    public long SoleTargetDamageGained => DamageGainedOn(UnfoldingDoomTargetOutcome.SoleTarget);

    /// <summary>Applications whose window offered more than one enemy.</summary>
    public int RatedApplications => Result.Applications.Count(application => application.Rated);

    /// <summary>Applications on their window's priority target, out of <see cref="RatedApplications"/>.</summary>
    public int PriorityApplications =>
        Result.Applications.Count(application => application.Rated && application.OnPriority);

    /// <summary>Milliseconds of the pull with Unfolding Doom available to cast.</summary>
    public int AvailableMs => Result.AvailableMs;

    /// <summary>Every stretch of the pull with Unfolding Doom available and no enemy debuffed.</summary>
    public IReadOnlyList<AuraWindow> IdleWindows => Result.IdleWindows;

    /// <summary>Milliseconds of the pull with Unfolding Doom available and no enemy debuffed.</summary>
    public int IdleAvailableMs => Result.IdleWindows.Sum(window => window.Duration);

    /// <summary>Share (0-1) of the pull spent available with no enemy debuffed.</summary>
    public double IdleAvailableShare => Pull.Duration > 0 ? IdleAvailableMs / (double)Pull.Duration : 0;

    /// <summary>Every reapplication, in the order they were applied.</summary>
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

    [On<DeathEvent>]
    private void OnDeath(DeathEvent e)
    {
        var unit = AuraWindowLedger.KeyOf(e);

        if (!_deaths.TryGetValue(unit, out var recorded) || e.Timestamp < recorded)
            _deaths[unit] = e.Timestamp;
    }

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
            var stretches = new List<AuraWindow>();
            foreach (var window in windows)
            {
                if (window.Duration <= 0) continue;

                stretches.AddRange(SplitAtReapplications(unit, window));
            }

            stretches.Sort((left, right) => left.Start.CompareTo(right.Start));

            var damage = new long[stretches.Count];
            var gained = new long[stretches.Count];

            foreach (var e in _playerDamage)
            {
                if (AuraWindowLedger.KeyOf(e) != unit) continue;

                for (var i = 0; i < stretches.Count; i++)
                {
                    if (e.Timestamp < stretches[i].Start || e.Timestamp > stretches[i].End) continue;

                    damage[i] += e.Amount + (e.Absorbed ?? 0);
                    gained[i] += CombatMath.CalculateEffectiveDamage(e, DamageIncrease);
                    break;
                }
            }

            for (var i = 0; i < stretches.Count; i++)
                applications.Add(BuildApplication(unit, stretches[i], damage[i], gained[i], idle));
        }

        applications.Sort((left, right) => left.Start.CompareTo(right.Start));

        return new Computed(
            activeMs,
            duration > 0 ? Math.Min(1d, activeMs / (double)duration) : 0d,
            applications,
            available.Sum(window => window.Duration),
            idle);
    }

    private UnfoldingDoomApplication BuildApplication(
        UnitKey unit,
        AuraWindow stretch,
        long damage,
        long gained,
        List<AuraWindow> idle)
    {
        var windowEnd = Math.Min(stretch.Start + DebuffDurationMs, Pull.EndTime);
        var order = RankTarget(unit, stretch.Start, windowEnd);
        var died = _deaths.TryGetValue(unit, out var timestamp) && timestamp > stretch.Start && timestamp <= windowEnd
            ? timestamp - stretch.Start
            : (int?)null;

        return new UnfoldingDoomApplication(
            unit,
            stretch.Start,
            stretch.End,
            damage,
            gained,
            DelayBefore(idle, stretch.Start),
            Classify(order.Rank, order.Candidates),
            order.Rank,
            order.Candidates,
            order.WindowDamage,
            order.BestWindowDamage,
            order.BestUnit,
            died);
    }

    private TargetOrder RankTarget(UnitKey unit, int start, int end)
    {
        var totals = new Dictionary<UnitKey, long> { [unit] = 0 };

        foreach (var e in _playerDamage)
        {
            if (e.Timestamp < start || e.Timestamp > end) continue;

            var key = AuraWindowLedger.KeyOf(e);
            totals[key] = totals.GetValueOrDefault(key) + e.Amount + (e.Absorbed ?? 0);
        }

        var chosen = totals[unit];
        var rank = 0;
        var best = chosen;
        var bestUnit = unit;

        foreach (var (key, total) in totals)
        {
            if (total <= chosen) continue;

            rank++;
            if (total <= best) continue;

            best = total;
            bestUnit = key;
        }

        return new TargetOrder(rank, totals.Count, chosen, best, bestUnit);
    }

    private static UnfoldingDoomTargetOutcome Classify(int rank, int candidates) =>
        candidates <= 1 ? UnfoldingDoomTargetOutcome.SoleTarget
        : rank == 0 ? UnfoldingDoomTargetOutcome.Priority
        : UnfoldingDoomTargetOutcome.Alternate;

    private long DamageGainedOn(UnfoldingDoomTargetOutcome outcome) =>
        Result.Applications
            .Where(application => application.Outcome == outcome)
            .Sum(application => application.DamageGained);

    private List<AuraWindow> SplitAtReapplications(UnitKey unit, AuraWindow window)
    {
        var cuts = new List<int>();

        foreach (var entry in _reapplications)
        {
            if (entry.Unit != unit) continue;
            if (entry.Timestamp <= window.Start || entry.Timestamp >= window.End) continue;

            cuts.Add(entry.Timestamp);
        }

        if (cuts.Count == 0) return [window];

        cuts.Sort();

        var stretches = new List<AuraWindow>(cuts.Count + 1);
        var start = window.Start;

        foreach (var cut in cuts)
        {
            if (cut > start) stretches.Add(new AuraWindow(start, cut));

            start = cut;
        }

        if (window.End > start) stretches.Add(new AuraWindow(start, window.End));

        return stretches;
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
    /// The stretches of <paramref name="windows"/> that no block of <paramref name="subtract"/> overlaps,
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

    private readonly record struct TargetOrder(
        int Rank,
        int Candidates,
        long WindowDamage,
        long BestWindowDamage,
        UnitKey BestUnit);

    private sealed record Computed(
        int ActiveMs,
        double Uptime,
        List<UnfoldingDoomApplication> Applications,
        int AvailableMs,
        List<AuraWindow> IdleWindows);
}

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Measures how continuously Elarion kept Lunarlight Mark on the enemies of a pull, and what the
/// mark was paired with. The mark debuff lasts 15 seconds while <see cref="Spells.LunarlightMark"/>
/// itself recharges over 40, so the ability alone cannot sustain it: the secondary application
/// paths (a Celestial Shot spending Celestial Impetus, spirit refund procs) carry the rest.
/// Applications landing within <see cref="CastAttributionWindowMs"/> of a Lunarlight Mark cast are
/// attributed to that cast; every other application is secondary.
/// <para>
/// Presence is tracked per (TargetId, TargetInstance) from apply, refresh, and remove events only.
/// Stack events move stacks within an existing mark rather than its presence, so they never open or
/// close a window. Uptime is the union of every target's windows against the pull duration, so two
/// marked enemies at once are not counted twice. A window still open at pull end is closed at
/// <see cref="Pull.EndTime"/>: a non-selected enemy that dies carrying the mark logs no removal, so
/// an open window is a live mark rather than a gap.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class LunarlightMarkAnalyzer : Analyzer
{
    /// <summary>
    /// A mark application landing this soon after a Lunarlight Mark cast is credited to the cast.
    /// Kept tight because the secondary paths fire off unrelated casts throughout the pull.
    /// </summary>
    public const int CastAttributionWindowMs = 250;

    private readonly Dictionary<(int TargetId, int TargetInstance), int> _openWindowStarts = [];
    private readonly List<MarkWindow> _closedWindows = [];
    private readonly List<int> _markCastTimestamps = [];

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>Pull length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    /// <summary>Milliseconds during which at least one enemy carried the mark.</summary>
    public int MarkedUptimeMs => Result.MarkedUptimeMs;

    /// <summary>Share of the pull (0-100) during which at least one enemy carried the mark.</summary>
    public double MarkedUptimePercentage =>
        PullDurationMs == 0 ? 0 : Math.Min(100d, MarkedUptimeMs / (double)PullDurationMs * 100);

    /// <summary>Every mark window of the pull, ordered by start time.</summary>
    public IReadOnlyList<MarkWindow> Windows => Result.Windows;

    /// <summary>Distinct enemies that carried the mark at some point during the pull.</summary>
    public int DistinctMarkedTargets => Result.DistinctTargets;

    /// <summary>Lunarlight Mark casts made during the pull.</summary>
    public int MarkCasts => _markCastTimestamps.Count;

    /// <summary>Mark applications landing within the attribution window of a Lunarlight Mark cast.</summary>
    public int MarkCastApplications { get; private set; }

    /// <summary>Mark applications from every other source: Celestial Impetus spends, spirit procs, talents.</summary>
    public int SecondaryApplications { get; private set; }

    /// <summary>All mark applications observed during the pull, refreshes excluded.</summary>
    public int TotalApplications => MarkCastApplications + SecondaryApplications;

    /// <summary>Share of applications (0-100) that came from a path other than the Lunarlight Mark cast.</summary>
    public double SecondaryApplicationPercentage =>
        TotalApplications == 0 ? 0 : SecondaryApplications / (double)TotalApplications * 100;

    /// <summary>Heartseeker Barrage casts made during the pull.</summary>
    public int BarrageCasts { get; private set; }

    /// <summary>Heartseeker Barrage casts started on a target that was carrying the mark.</summary>
    public int BarrageCastsOnMarkedTarget { get; private set; }

    /// <summary>Share of Heartseeker Barrage casts (0-100) channelled into a marked target.</summary>
    public double BarrageOnMarkedPercentage =>
        BarrageCasts == 0 ? 0 : BarrageCastsOnMarkedTarget / (double)BarrageCasts * 100;

    /// <summary>Lunarlight Salvo hits, the single-target payoff of a marked enemy.</summary>
    public int SalvoHits { get; private set; }

    /// <summary>Lunarlight Salvo: Erupt hits, the area payoff of a marked enemy.</summary>
    public int EruptHits { get; private set; }

    /// <summary>Salvo and Erupt hits combined.</summary>
    public int TotalSalvoHits => SalvoHits + EruptHits;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMark))]
    private void OnMarkCast(CastEvent e) => _markCastTimestamps.Add(e.Timestamp);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkDebuff))]
    private void OnMarkApplied(ApplyDebuffEvent e)
    {
        OpenWindow(e);

        if (IsCastAttributed(e.Timestamp))
            MarkCastApplications++;
        else
            SecondaryApplications++;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkDebuff))]
    private void OnMarkRefreshed(RefreshDebuffEvent e) => OpenWindow(e);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkDebuff))]
    private void OnMarkRemoved(RemoveDebuffEvent e)
    {
        var key = Key(e.TargetId, e.TargetInstance);
        if (!_openWindowStarts.Remove(key, out var start))
            return;

        _closedWindows.Add(new MarkWindow(key.TargetId, key.TargetInstance, start, e.Timestamp, true));
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartseekerBarrage))]
    private void OnBarrageCast(CastEvent e)
    {
        BarrageCasts++;
        if (_openWindowStarts.ContainsKey(Key(e.TargetId, e.TargetInstance)))
            BarrageCastsOnMarkedTarget++;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkDamage))]
    private void OnSalvoDamage(DamageEvent e) => SalvoHits++;

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkAoeDamage))]
    private void OnEruptDamage(DamageEvent e) => EruptHits++;

    private void OpenWindow(BuffEvent e) => _openWindowStarts.TryAdd(Key(e.TargetId, e.TargetInstance), e.Timestamp);

    private bool IsCastAttributed(int timestamp)
    {
        if (_markCastTimestamps.Count == 0)
            return false;

        var cast = _markCastTimestamps[^1];
        return timestamp >= cast && timestamp - cast <= CastAttributionWindowMs;
    }

    private static (int TargetId, int TargetInstance) Key(int targetId, int? targetInstance) =>
        (targetId, targetInstance ?? 0);

    private Computed Compute()
    {
        List<MarkWindow> windows = [.. _closedWindows];
        foreach (var (key, start) in _openWindowStarts)
            windows.Add(new MarkWindow(key.TargetId, key.TargetInstance, start, Pull.EndTime, false));

        windows.Sort(static (left, right) =>
        {
            var byStart = left.StartMs.CompareTo(right.StartMs);
            if (byStart != 0)
                return byStart;

            var byTarget = left.TargetId.CompareTo(right.TargetId);
            return byTarget != 0 ? byTarget : left.TargetInstance.CompareTo(right.TargetInstance);
        });

        var union = 0;
        var coveredTo = int.MinValue;
        foreach (var window in windows)
        {
            if (window.EndMs <= coveredTo)
                continue;

            union += window.EndMs - Math.Max(window.StartMs, coveredTo);
            coveredTo = window.EndMs;
        }

        var distinct = new HashSet<(int, int)>();
        foreach (var window in windows)
            distinct.Add((window.TargetId, window.TargetInstance));

        return new Computed(windows, union, distinct.Count);
    }

    /// <summary>
    /// One continuous stretch of Lunarlight Mark on one enemy, in absolute timestamps.
    /// <paramref name="ClosedByRemoval"/> is <c>false</c> when the window was still open at pull end,
    /// which happens when the enemy died carrying the mark.
    /// </summary>
    public sealed record MarkWindow(
        int TargetId,
        int TargetInstance,
        int StartMs,
        int EndMs,
        bool ClosedByRemoval);

    private readonly record struct Computed(
        IReadOnlyList<MarkWindow> Windows,
        int MarkedUptimeMs,
        int DistinctTargets);
}

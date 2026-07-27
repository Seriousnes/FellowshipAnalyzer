using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class LunarlightMarkAnalyzer : Analyzer
{
    public const int CastAttributionWindowMs = 250;

    private readonly Dictionary<(int TargetId, int TargetInstance), int> _openWindowStarts = [];
    private readonly List<MarkWindow> _closedWindows = [];
    private readonly List<int> _markCastTimestamps = [];
    private Computed Result => field ??= Compute();

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    public int MarkedUptimeMs => Result.MarkedUptimeMs;

    public double MarkedUptimePercentage =>
        PullDurationMs == 0 ? 0 : Math.Min(100d, MarkedUptimeMs / (double)PullDurationMs * 100);

    public IReadOnlyList<MarkWindow> Windows => Result.Windows;

    public int DistinctMarkedTargets => Result.DistinctTargets;

    public int MarkCasts => _markCastTimestamps.Count;

    public int MarkCastApplications { get; private set; }

    public int SecondaryApplications { get; private set; }

    public int TotalApplications => MarkCastApplications + SecondaryApplications;

    public double SecondaryApplicationPercentage =>
        TotalApplications == 0 ? 0 : SecondaryApplications / (double)TotalApplications * 100;

    public int BarrageCasts { get; private set; }

    public int BarrageCastsOnMarkedTarget { get; private set; }

    public double BarrageOnMarkedPercentage =>
        BarrageCasts == 0 ? 0 : BarrageCastsOnMarkedTarget / (double)BarrageCasts * 100;

    public int SalvoHits { get; private set; }

    public int EruptHits { get; private set; }

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

    public sealed record MarkWindow(
        int TargetId,
        int TargetInstance,
        int StartMs,
        int EndMs,
        bool ClosedByRemoval);

    private record Computed(
        IReadOnlyList<MarkWindow> Windows,
        int MarkedUptimeMs,
        int DistinctTargets);
}

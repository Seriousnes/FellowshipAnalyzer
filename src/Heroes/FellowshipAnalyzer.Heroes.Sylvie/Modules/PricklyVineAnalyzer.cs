using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class PricklyVineAnalyzer : Analyzer
{
    private readonly List<int> _vineCasts = [];

    private Computed Result => field ??= Compute();

    public int VineCasts => _vineCasts.Count;

    public double AverageLiveVines => Result.AverageVines;

    public int PeakLiveVines => Result.PeakVines;

    public int NoVineMs => Result.NoVineMs;

    public double NoVineShare => PullDurationMs > 0 ? NoVineMs / (double)PullDurationMs : 0;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.PricklyVine))]
    private void OnVineCast(CastEvent castEvent) => _vineCasts.Add(castEvent.Timestamp);

    private Computed Compute()
    {
        if (PullDurationMs == 0) return new Computed(0, 0, 0);

        var changes = new List<(int Timestamp, int Delta)>(_vineCasts.Count * 2);
        foreach (var cast in _vineCasts)
        {
            changes.Add((cast + SylvieKit.PricklyVineSpawnMs, 1));
            changes.Add((cast + SylvieKit.PricklyVineSpawnMs + SylvieKit.PricklyVineDurationMs, -1));
        }

        changes.Sort(static (left, right) => left.Timestamp.CompareTo(right.Timestamp));

        var live = 0;
        var peak = 0;
        var noVineMs = 0;
        long weighted = 0;
        var cursor = Pull.StartTime;

        foreach (var (timestamp, delta) in changes)
        {
            var at = Math.Clamp(timestamp, Pull.StartTime, Pull.EndTime);
            var elapsed = at - cursor;
            if (elapsed > 0)
            {
                weighted += (long)live * elapsed;
                if (live == 0) noVineMs += elapsed;
                cursor = at;
            }

            live += delta;
            peak = Math.Max(peak, live);
        }

        var tail = Pull.EndTime - cursor;
        if (tail > 0)
        {
            weighted += (long)live * tail;
            if (live == 0) noVineMs += tail;
        }

        return new Computed(weighted / (double)PullDurationMs, peak, noVineMs);
    }

    private sealed record Computed(double AverageVines, int PeakVines, int NoVineMs);
}

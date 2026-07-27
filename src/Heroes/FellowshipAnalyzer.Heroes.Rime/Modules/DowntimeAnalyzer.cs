using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class DowntimeAnalyzer : Analyzer
{
    public const int MinimumGapMs = 500;

    public const int TopGapLimit = 10;

    public const int FallbackGcdMs = 1500;

    private readonly List<ActivityWindow> _windows = [];
    private readonly Dictionary<CastEvent, int> _castStarts = [];
    private readonly List<DowntimeGap> _gaps = [];

    private bool _materialized;
    private int _measuredSpanMs;
    private int _activeMs;
    private int _gapCount;
    private int _longestGapMs;

    public int MeasuredSpanMs { get { EnsureMaterialized(); return _measuredSpanMs; } }

    public int ActiveMs { get { EnsureMaterialized(); return _activeMs; } }

    public int DowntimeMs { get { EnsureMaterialized(); return Math.Max(0, _measuredSpanMs - _activeMs); } }

    public double ActiveRatio
    {
        get
        {
            EnsureMaterialized();
            return _measuredSpanMs <= 0 ? 1 : Math.Clamp((double)_activeMs / _measuredSpanMs, 0, 1);
        }
    }

    public int GapCount { get { EnsureMaterialized(); return _gapCount; } }

    public int LongestGapMs { get { EnsureMaterialized(); return _longestGapMs; } }

    public IReadOnlyList<DowntimeGap> Gaps { get { EnsureMaterialized(); return _gaps; } }

    [On<BeginCastEvent>(By = Actor.Player)]
    private void OnBeginCast(BeginCastEvent beginCastEvent)
    {
        if (beginCastEvent.CastEvent is { } cast)
            _castStarts[cast] = beginCastEvent.Timestamp;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.GlobalCooldown is not { } gcd)
            return;

        var start = _castStarts.TryGetValue(castEvent, out var castStart)
            ? Math.Min(castStart, castEvent.Timestamp)
            : castEvent.Timestamp;
        _castStarts.Remove(castEvent);

        var end = castEvent.Timestamp + BusyMs(gcd);
        if (castEvent.Channel is { } endChannel)
            end = Math.Max(end, endChannel.Timestamp);

        _windows.Add(new ActivityWindow(start, end));
    }

    [On<BeginChannelEvent>(By = Actor.Player)]
    private void OnBeginChannel(BeginChannelEvent beginChannelEvent)
    {
        if (beginChannelEvent.GlobalCooldown is not { } gcd)
            return;

        var end = beginChannelEvent.Timestamp + BusyMs(gcd);
        if (beginChannelEvent.EndChannel is { } endChannel)
            end = Math.Max(end, endChannel.Timestamp);

        _windows.Add(new ActivityWindow(beginChannelEvent.Timestamp, end));
    }

    private static int BusyMs(GlobalCooldownEvent gcd) => gcd.Duration > 0 ? gcd.Duration : FallbackGcdMs;

    private void EnsureMaterialized()
    {
        if (_materialized) return;
        _materialized = true;

        var merged = MergeWindows();
        if (merged.Count == 0)
            return;

        var spanStart = merged[0].Start;
        _measuredSpanMs = Math.Max(0, Pull.EndTime - spanStart);

        var allGaps = new List<DowntimeGap>();
        var cursor = spanStart;

        foreach (var window in merged)
        {
            AccumulateIdle(allGaps, cursor, window.Start);
            _activeMs += window.End - window.Start;
            cursor = window.End;
        }

        AccumulateIdle(allGaps, cursor, Pull.EndTime);

        _gapCount = allGaps.Count;
        _longestGapMs = allGaps.Count == 0 ? 0 : allGaps.Max(gap => gap.DurationMs);
        _gaps.AddRange(allGaps.OrderByDescending(gap => gap.DurationMs).ThenBy(gap => gap.StartTimestamp).Take(TopGapLimit));
    }

    private void AccumulateIdle(List<DowntimeGap> gaps, int from, int to)
    {
        var idleMs = to - from;
        if (idleMs <= 0)
            return;

        if (idleMs < MinimumGapMs)
        {
            _activeMs += idleMs;
            return;
        }

        gaps.Add(new DowntimeGap(from, idleMs));
    }

    private List<ActivityWindow> MergeWindows()
    {
        var clamped = new List<ActivityWindow>(_windows.Count);
        foreach (var window in _windows)
        {
            var start = Math.Clamp(window.Start, Pull.StartTime, Pull.EndTime);
            var end = Math.Clamp(window.End, Pull.StartTime, Pull.EndTime);
            if (end > start)
                clamped.Add(new ActivityWindow(start, end));
        }

        clamped.Sort(static (left, right) => left.Start.CompareTo(right.Start));

        var merged = new List<ActivityWindow>(clamped.Count);
        foreach (var window in clamped)
        {
            if (merged.Count > 0 && window.Start <= merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, window.End) };
                continue;
            }

            merged.Add(window);
        }

        return merged;
    }

    private readonly record struct ActivityWindow(int Start, int End);
}

public sealed record DowntimeGap(int StartTimestamp, int DurationMs);

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Measures how much of a pull Rime spent with the global cooldown rolling. Every second the GCD
/// sits idle is Anima and Winter Orb generation that never happened, and Rime's generators are
/// cheap enough that there is always something to press between mechanics, so idle GCD time reads
/// as straight throughput loss. This analyzer measures cast uptime only; it says nothing about
/// which ability should have filled a gap.
/// </summary>
/// <remarks>
/// An activity window is opened by any player event carrying a fabricated
/// <see cref="GlobalCooldownEvent"/>: a <see cref="CastEvent"/> for an on-GCD cast and a
/// <see cref="BeginChannelEvent"/> for an on-GCD channel, which is where the core
/// <c>GlobalCooldown</c> module attaches a channel's GCD. Casts with no GCD event are off-GCD
/// utility (Ice Dash, Frost Ward, Brain Freeze) and neither open a window nor split one. No event
/// is fabricated here, so nothing is back-dated; the windows are derived entirely from links the
/// normalizers already established.
/// <para>
/// A window runs from the start of the action to the end of the commitment it created:
/// <list type="bullet">
///   <item>
///     It starts at the linked <see cref="BeginCastEvent"/> when the cast completed a cast bar,
///     because a <see cref="CastEvent"/> is logged at cast completion and the time spent casting is
///     activity, not idle. Instants and channels start at their own timestamp.
///   </item>
///   <item>
///     It ends at the later of the GCD expiring and the channel finishing, read from the linked
///     <see cref="EndChannelEvent"/>'s timestamp. That event's own <c>Start</c> and <c>Duration</c>
///     fields are not populated by Fellowship Logs, so the channel end is the only usable reading,
///     and a channel with no logged end falls back to its GCD.
///   </item>
/// </list>
/// Windows are clamped to the pull, merged, and then walked in start order, so a back-dated cast
/// bar overlapping an earlier window cannot invent a gap.
/// </para>
/// <para>
/// The measured span runs from the later of the pull start and the first activity window to the
/// pull end, so a pull is never charged for time before the player's first action. Idle stretches
/// shorter than <see cref="MinimumGapMs"/> are latency and reaction noise rather than lost casts
/// and are counted as active, including a short idle tail at the pull end, which keeps
/// <see cref="DowntimeMs"/> exactly equal to the total of every recorded gap.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class DowntimeAnalyzer : Analyzer
{
    /// <summary>Idle stretches shorter than this are treated as latency noise and counted as active.</summary>
    public const int MinimumGapMs = 500;

    /// <summary>How many gaps <see cref="Gaps"/> surfaces; <see cref="GapCount"/> and <see cref="DowntimeMs"/> cover every gap.</summary>
    public const int TopGapLimit = 10;

    /// <summary>Busy window length used when an anchoring event carries no usable GCD duration.</summary>
    public const int FallbackGcdMs = 1500;

    private readonly List<ActivityWindow> _windows = [];
    private readonly Dictionary<CastEvent, int> _castStarts = [];
    private readonly List<DowntimeGap> _gaps = [];

    private bool _materialized;
    private int _measuredSpanMs;
    private int _activeMs;
    private int _gapCount;
    private int _longestGapMs;

    /// <summary>Milliseconds from the first activity window to the pull end.</summary>
    public int MeasuredSpanMs { get { EnsureMaterialized(); return _measuredSpanMs; } }

    /// <summary>Milliseconds of <see cref="MeasuredSpanMs"/> spent casting, channelling, or on the GCD.</summary>
    public int ActiveMs { get { EnsureMaterialized(); return _activeMs; } }

    /// <summary>Milliseconds of <see cref="MeasuredSpanMs"/> spent idle across every recorded gap.</summary>
    public int DowntimeMs { get { EnsureMaterialized(); return Math.Max(0, _measuredSpanMs - _activeMs); } }

    /// <summary>Share of the measured span spent active, from 0 to 1; 1 when there is nothing to measure.</summary>
    public double ActiveRatio
    {
        get
        {
            EnsureMaterialized();
            return _measuredSpanMs <= 0 ? 1 : Math.Clamp((double)_activeMs / _measuredSpanMs, 0, 1);
        }
    }

    /// <summary>Every idle stretch of at least <see cref="MinimumGapMs"/>, including a trailing idle at the pull end.</summary>
    public int GapCount { get { EnsureMaterialized(); return _gapCount; } }

    /// <summary>The longest single idle stretch in milliseconds.</summary>
    public int LongestGapMs { get { EnsureMaterialized(); return _longestGapMs; } }

    /// <summary>The <see cref="TopGapLimit"/> longest gaps, longest first.</summary>
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

/// <summary>One idle stretch where the global cooldown was rolling on nothing.</summary>
public sealed record DowntimeGap(int StartTimestamp, int DurationMs);

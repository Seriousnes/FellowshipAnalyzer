using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Per-target presence bookkeeping for one aura: open, close and observe calls in, closed windows
/// out. Shared by <see cref="DebuffUptimeAnalyzer"/>, which collapses the result to a primary
/// target, and <see cref="AllTargetUptimeAnalyzer"/>, which keeps every target.
/// <para>
/// A window left open when <see cref="Build"/> is called is closed at the last moment its target was
/// observed, so an aura the log stops reporting does not run to the end of the pull unearned.
/// </para>
/// </summary>
public sealed class AuraWindowLedger
{
    private readonly Dictionary<UnitKey, List<AuraWindow>> _windowsByTarget = [];
    private readonly Dictionary<UnitKey, int> _openStarts = [];
    private readonly Dictionary<UnitKey, int> _lastObserved = [];

    /// <summary>
    /// Opens a window on <paramref name="target"/> unless one is already open, and records the event
    /// as an observation of that target.
    /// </summary>
    public void Open(IHasTargetWithInstanceEvent target, int timestamp)
    {
        var key = KeyOf(target);
        _lastObserved[key] = timestamp;
        _openStarts.TryAdd(key, timestamp);
    }

    /// <summary>
    /// Closes the window open on <paramref name="target"/>, if any, and records the event as an
    /// observation of that target.
    /// </summary>
    public void Close(IHasTargetWithInstanceEvent target, int timestamp)
    {
        var key = KeyOf(target);
        _lastObserved[key] = timestamp;
        if (!_openStarts.Remove(key, out var start)) return;

        WindowsFor(key).Add(new AuraWindow(start, timestamp));
    }

    /// <summary>
    /// Records that <paramref name="target"/> was still in the log at <paramref name="timestamp"/>,
    /// without opening or closing anything.
    /// </summary>
    public void Observe(IHasTargetWithInstanceEvent target, int timestamp) =>
        _lastObserved[KeyOf(target)] = timestamp;

    /// <summary>
    /// Every target's windows in encounter order, with any still-open window closed at that target's
    /// last observation. Targets that never carried the aura are absent.
    /// </summary>
    public IReadOnlyDictionary<UnitKey, IReadOnlyList<AuraWindow>> Build()
    {
        var result = new Dictionary<UnitKey, IReadOnlyList<AuraWindow>>();
        foreach (var (key, windows) in _windowsByTarget)
            result[key] = windows.ToArray();

        foreach (var (key, start) in _openStarts)
        {
            var closed = new AuraWindow(start, Math.Max(start, _lastObserved.GetValueOrDefault(key)));
            result[key] = result.TryGetValue(key, out var existing) ? [.. existing, closed] : [closed];
        }

        return result;
    }

    /// <summary>The (target, instance) key <paramref name="target"/> is filed under.</summary>
    public static UnitKey KeyOf(IHasTargetWithInstanceEvent target) =>
        new(target.TargetId, target.TargetInstance ?? 0);

    /// <summary>Milliseconds covered by <paramref name="windows"/>, counting overlap once.</summary>
    public static int CoveredMs(IReadOnlyList<AuraWindow> windows)
    {
        if (windows.Count == 0) return 0;

        var ordered = windows.OrderBy(window => window.Start).ToArray();
        var covered = 0;
        var start = ordered[0].Start;
        var end = ordered[0].End;

        foreach (var window in ordered.AsSpan(1))
        {
            if (window.Start > end)
            {
                covered += end - start;
                (start, end) = (window.Start, window.End);
                continue;
            }

            end = Math.Max(end, window.End);
        }

        return covered + (end - start);
    }

    private List<AuraWindow> WindowsFor(UnitKey key)
    {
        if (_windowsByTarget.TryGetValue(key, out var windows)) return windows;

        windows = [];
        _windowsByTarget[key] = windows;
        return windows;
    }
}

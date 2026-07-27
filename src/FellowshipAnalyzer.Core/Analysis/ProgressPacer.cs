using System.Diagnostics;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Paces loading-progress updates by elapsed time rather than by iteration count, so the number of
/// re-renders and event-loop round trips a step performs stays constant as the event count grows.
/// <para>
/// Create one per loop and call <see cref="ShouldYield"/> with the loop index; when it returns
/// <see langword="true"/>, publish progress and yield. The clock is only read every
/// <see cref="CheckInterval"/> iterations, because under WebAssembly
/// <see cref="Stopwatch.GetTimestamp"/> crosses into JavaScript and is not free to call per event.
/// </para>
/// </summary>
public struct ProgressPacer
{
    /// <summary>How often the elapsed-time check is allowed to run, in loop iterations.</summary>
    public const int CheckInterval = 256;

    /// <summary>The rate a step publishes progress at while it runs, however many events it has left.</summary>
    public const int UpdatesPerSecond = 20;

    private const int CheckMask = CheckInterval - 1;

    private static readonly long IntervalTicks = Stopwatch.Frequency / UpdatesPerSecond;

    private long _lastYield;

    /// <summary>Starts the interval from the moment of construction.</summary>
    public ProgressPacer() => _lastYield = Stopwatch.GetTimestamp();

    /// <summary>
    /// Returns <see langword="true"/> when enough time has passed to be worth publishing progress and
    /// yielding to the browser, and arms the next interval.
    /// </summary>
    public bool ShouldYield(int iteration)
    {
        if ((iteration & CheckMask) != 0)
            return false;

        var now = Stopwatch.GetTimestamp();
        if (now - _lastYield < IntervalTicks)
            return false;

        _lastYield = now;
        return true;
    }
}

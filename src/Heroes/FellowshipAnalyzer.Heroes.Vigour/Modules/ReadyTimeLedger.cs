using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed class ReadyTimeLedger
{
    private readonly List<AuraWindow> _ready = [];
    private readonly List<AuraWindow> _excluded = [];
    private int? _readySince;
    private int? _excludedSince;

    public void Start(int timestamp, bool available)
    {
        if (available) _readySince = timestamp;
    }

    public void Observe(UpdateSpellUsableEvent e)
    {
        if (e.IsAvailable)
        {
            _readySince ??= e.Timestamp;
            return;
        }

        Close(ref _readySince, _ready, e.Timestamp);
    }

    public void Exclude(int timestamp) => _excludedSince ??= timestamp;

    public void Include(int timestamp) => Close(ref _excludedSince, _excluded, timestamp);

    public int ReadyMs(int start, int end)
    {
        var ready = Clip(_ready, _readySince, start, end);
        var excluded = Clip(_excluded, _excludedSince, start, end);

        var total = 0;
        foreach (var window in ready)
        {
            total += window.Duration;
            foreach (var cut in excluded)
                total -= Math.Max(0, Math.Min(window.End, cut.End) - Math.Max(window.Start, cut.Start));
        }

        return Math.Max(0, total);
    }

    public int ExcludedMs(int start, int end) => AuraWindowLedger.ActiveMs(Clip(_excluded, _excludedSince, start, end));

    private static void Close(ref int? since, List<AuraWindow> windows, int timestamp)
    {
        if (since is not { } start) return;

        if (timestamp > start) windows.Add(new AuraWindow(start, timestamp));
        since = null;
    }

    private static List<AuraWindow> Clip(List<AuraWindow> windows, int? openSince, int start, int end)
    {
        var all = openSince is { } open ? [.. windows, new AuraWindow(open, end)] : windows;
        return
        [
            .. all
                .Where(window => window.End > start && window.Start < end)
                .Select(window => new AuraWindow(Math.Max(window.Start, start), Math.Min(window.End, end))),
        ];
    }
}

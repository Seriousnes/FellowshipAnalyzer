using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SupremacyAnalyzer : Analyzer
{
    private readonly List<WindowState> _windows = [];

    public IReadOnlyList<SupremacyWindow> Windows =>
        field ??=
        [
            .. _windows.Select(window => new SupremacyWindow(
                window.Start,
                window.End ?? Pull.EndTime,
                window.MultishotCasts,
                window.FirstMultishot is { } first ? first - window.Start : null)),
        ];

    public int EmpoweredMultishotCasts => Windows.Sum(window => window.MultishotCasts);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersSupremacyBuff))]
    private void OnSupremacyApplied(ApplyBuffEvent e)
    {
        if (OpenWindow() is { } previous)
            previous.End = e.Timestamp;

        _windows.Add(new WindowState { Start = e.Timestamp });
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Multishot))]
    private void OnMultishotCast(CastEvent e)
    {
        if (OpenWindow() is not { } window)
            return;

        window.MultishotCasts++;
        window.FirstMultishot ??= e.Timestamp;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersSupremacyBuff))]
    private void OnSupremacyRemoved(RemoveBuffEvent e)
    {
        if (OpenWindow() is { } window)
            window.End = e.Timestamp;
    }

    private WindowState? OpenWindow()
    {
        var last = _windows.Count > 0 ? _windows[^1] : null;
        return last is { End: null } ? last : null;
    }

    public sealed record SupremacyWindow(
        int StartMs,
        int EndMs,
        int MultishotCasts,
        int? FirstMultishotDelayMs);

    private sealed class WindowState
    {
        public int Start { get; init; }
        public int? End { get; set; }
        public int MultishotCasts { get; set; }
        public int? FirstMultishot { get; set; }
    }
}

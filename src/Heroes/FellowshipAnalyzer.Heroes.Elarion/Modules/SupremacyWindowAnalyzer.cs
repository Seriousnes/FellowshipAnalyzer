using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SupremacyWindowAnalyzer : Analyzer
{
    public const int ConsumeWindowMs = 150;

    private readonly List<MultishotCast> _multishotCasts = [];
    private readonly List<WindowState> _windows = [];

    public int SupremacyCasts { get; private set; }

    public int MultishotCasts => _multishotCasts.Count;

    public int EmpoweredMultishotCasts { get; private set; }

    public int RegularMultishotCasts => MultishotCasts - EmpoweredMultishotCasts;

    public IReadOnlyList<SupremacyWindow> Windows =>
        field ??=
        [
            .. _windows.Select(window => new SupremacyWindow(
                window.Start,
                window.End ?? Pull.EndTime,
                window.Granted,
                window.Consumed,
                Math.Max(0, window.Granted - window.Consumed),
                window.End is null || window.ClosedByExpiry)),
        ];

    public int StacksGranted => Windows.Sum(window => window.StacksGranted);

    public int StacksConsumed => Windows.Sum(window => window.StacksConsumed);

    public int StacksWasted => Windows.Sum(window => window.StacksWasted);

    public int WindowsFullyDrained => Windows.Count(window => window.StacksWasted == 0);

    public double StacksConsumedPercentage =>
        StacksGranted == 0 ? 0 : StacksConsumed / (double)StacksGranted * 100;

    public bool TalentedFerventSupremacy =>
        Owner.SelectedCombatant.HasTalent(ElarionTalents.FerventSupremacy);

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SkystridersSupremacy))]
    private void OnSupremacyCast(CastEvent e) => SupremacyCasts++;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Multishot))]
    private void OnMultishotCast(CastEvent e)
    {
        if (OpenWindow() is { Remaining: > 0 })
            EmpoweredMultishotCasts++;

        _multishotCasts.Add(new MultishotCast(e.Timestamp));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersSupremacyBuff))]
    private void OnSupremacyApplied(ApplyBuffEvent e)
    {
        if (OpenWindow() is { } previous)
            CloseWindow(previous, e.Timestamp, byExpiry: true);

        _windows.Add(new WindowState { Start = e.Timestamp, Granted = 1, Remaining = 1 });
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersSupremacyBuff))]
    private void OnSupremacyStackApplied(ApplyBuffStackEvent e)
    {
        if (OpenWindow() is not { } window)
            return;

        window.Granted += Math.Max(0, e.Stack - window.Remaining);
        window.Remaining = e.Stack;
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersSupremacyBuff))]
    private void OnSupremacyStackRemoved(RemoveBuffStackEvent e)
    {
        if (OpenWindow() is not { } window)
            return;

        var lost = Math.Max(0, window.Remaining - e.Stack);
        window.Remaining = e.Stack;
        if (lost > 0 && ClaimCast(e.Timestamp))
            window.Consumed++;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersSupremacyBuff))]
    private void OnSupremacyRemoved(RemoveBuffEvent e)
    {
        if (OpenWindow() is not { } window)
            return;

        var lost = window.Remaining;
        var spent = lost > 0 && ClaimCast(e.Timestamp);
        if (spent)
            window.Consumed++;

        CloseWindow(window, e.Timestamp, byExpiry: !spent);
    }

    private static void CloseWindow(WindowState window, int timestamp, bool byExpiry)
    {
        window.Remaining = 0;
        window.End = timestamp;
        window.ClosedByExpiry = byExpiry;
    }

    private WindowState? OpenWindow()
    {
        var last = _windows.Count > 0 ? _windows[^1] : null;
        return last is { End: null } ? last : null;
    }

    private bool ClaimCast(int timestamp)
    {
        for (var i = _multishotCasts.Count - 1; i >= 0; i--)
        {
            var cast = _multishotCasts[i];
            var elapsed = timestamp - cast.Timestamp;
            if (elapsed > ConsumeWindowMs)
                break;

            if (elapsed < 0 || cast.Claimed)
                continue;

            cast.Claimed = true;
            return true;
        }

        return false;
    }

    public sealed record SupremacyWindow(
        int StartMs,
        int EndMs,
        int StacksGranted,
        int StacksConsumed,
        int StacksWasted,
        bool ClosedByExpiry)
    {
        public int DurationMs => Math.Max(0, EndMs - StartMs);
    }

    private sealed class WindowState
    {
        public int Start { get; init; }
        public int Granted { get; set; }
        public int Remaining { get; set; }
        public int Consumed { get; set; }
        public int? End { get; set; }
        public bool ClosedByExpiry { get; set; }
    }

    private sealed class MultishotCast(int timestamp)
    {
        public int Timestamp { get; } = timestamp;

        public bool Claimed { get; set; }
    }
}

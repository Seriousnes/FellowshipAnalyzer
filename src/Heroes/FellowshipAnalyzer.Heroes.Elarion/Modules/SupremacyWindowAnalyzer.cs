using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Measures how each <see cref="Spells.SkystridersSupremacy"/> window was drained. Casting Supremacy
/// grants <see cref="Spells.SkystridersSupremacyBuff"/> with a full load of stacks, and every
/// <see cref="Spells.Multishot"/> fired while the buff is up spends one of them on an empowered cast
/// that hits far harder and costs half the Focus. A window that times out with stacks left is
/// empowerment thrown away.
/// <para>
/// Windows are read from the player's own buff stream. The buff arrives as an apply immediately
/// followed by a stack event carrying the full load, so the stack count is taken as an absolute
/// reading and the window still opens on a single stack when that second event is missing. Each
/// Multishot is logged before the stack loss it causes, so the count read at a cast is the count going
/// into it. A stack loss within <see cref="ConsumeWindowMs"/> after a Multishot cast is a spend and
/// every other loss is waste, matched one to one so a single cast cannot claim two losses. A window
/// with no removal at all stays open to <see cref="Pull"/> end.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SupremacyWindowAnalyzer : Analyzer
{
    /// <summary>
    /// A stack loss this soon after a Multishot cast counts as spent rather than wasted. Kept tight
    /// because Multishot is cast far more often than Supremacy comes up, so a wide window would read a
    /// genuine expiry beside an unrelated cast as a spend.
    /// </summary>
    public const int ConsumeWindowMs = 150;

    private readonly List<MultishotCast> _multishotCasts = [];
    private readonly List<WindowState> _windows = [];
    private List<SupremacyWindow>? _projectedWindows;

    /// <summary>Skystrider's Supremacy casts made during the pull.</summary>
    public int SupremacyCasts { get; private set; }

    /// <summary>Multishot casts made during the pull, empowered or not.</summary>
    public int MultishotCasts => _multishotCasts.Count;

    /// <summary>Multishot casts started with a Supremacy stack banked, so the cast was empowered.</summary>
    public int EmpoweredMultishotCasts { get; private set; }

    /// <summary>Multishot casts started with no Supremacy stack to spend.</summary>
    public int RegularMultishotCasts => MultishotCasts - EmpoweredMultishotCasts;

    /// <summary>One entry per Supremacy window, in the order they opened.</summary>
    public IReadOnlyList<SupremacyWindow> Windows =>
        _projectedWindows ??=
        [
            .. _windows.Select(window => new SupremacyWindow(
                window.Start,
                window.End ?? Pull.EndTime,
                window.Granted,
                window.Consumed,
                Math.Max(0, window.Granted - window.Consumed),
                window.End is null || window.ClosedByExpiry)),
        ];

    /// <summary>Supremacy stacks granted across every window this pull.</summary>
    public int StacksGranted => Windows.Sum(window => window.StacksGranted);

    /// <summary>Stacks spent on an empowered Multishot across every window this pull.</summary>
    public int StacksConsumed => Windows.Sum(window => window.StacksConsumed);

    /// <summary>Stacks that never became an empowered Multishot across every window this pull.</summary>
    public int StacksWasted => Windows.Sum(window => window.StacksWasted);

    /// <summary>Windows that were emptied of stacks by Multishot casts.</summary>
    public int WindowsFullyDrained => Windows.Count(window => window.StacksWasted == 0);

    /// <summary>Share of granted stacks (0-100) spent on an empowered Multishot.</summary>
    public double StacksConsumedPercentage =>
        StacksGranted == 0 ? 0 : StacksConsumed / (double)StacksGranted * 100;

    /// <summary>Whether the build takes Fervent Supremacy, which reshapes the window.</summary>
    public bool TalentedFerventSupremacy =>
        Owner.SelectedCombatant.HasTalent(ElarionTalents.FerventSupremacy);

    /// <summary>Pull length in milliseconds.</summary>
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

    /// <summary>
    /// One Skystrider's Supremacy window. <paramref name="ClosedByExpiry"/> is true when the buff ran
    /// out rather than being emptied by a Multishot, which includes a window still open at pull end.
    /// </summary>
    public sealed record SupremacyWindow(
        int StartMs,
        int EndMs,
        int StacksGranted,
        int StacksConsumed,
        int StacksWasted,
        bool ClosedByExpiry)
    {
        /// <summary>How long the window lasted, in milliseconds.</summary>
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

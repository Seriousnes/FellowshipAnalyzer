using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Measures how tightly Gunde stacked his three burst cooldowns into the same window. Reign in Blood
/// is the amplifier the whole burst is built around: for 12 seconds 30% more damage flows into Rend,
/// and it sits on a long cooldown. Bloodbound Spirit (a 20 second self-buff worth 15% damage) and
/// Rupture (the single biggest hit, and the Open Wounds application) should both land inside that
/// window so their damage is amplified, and held Blood Feather stacks should be deployed there
/// through Owed in Blood. Spreading the three across the fight instead is the failure this analyzer
/// names.
/// </summary>
/// <remarks>
/// Windows are anchored on the Reign in Blood self-buff rather than on its cast, so a window reflects
/// the amplifier actually being live. Bloodbound Spirit is commonly pre-cast and Reign in Blood
/// triggered on top of it, so a cast up to <see cref="LeadInMs"/> before the buff lands still belongs
/// to the window. The per-window presence flags claim each cast for at most one window, earliest
/// window first, so a single Rupture can never satisfy two windows. Assignment runs once when the
/// pull's results are first read.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class BurstWindowAnalyzer : Analyzer
{
    /// <summary>Data-verified duration of the Reign in Blood self-buff, used only when no removal was logged.</summary>
    public const int BuffDurationMs = 12_000;

    /// <summary>Grace period before a window's start in which a cast still counts as part of the burst.</summary>
    public const int LeadInMs = 3_000;

    private readonly List<RawWindow> _rawWindows = [];
    private readonly List<int> _ruptures = [];
    private readonly List<int> _bloodboundSpirits = [];
    private readonly List<int> _owedInBloods = [];

    private RawWindow? _open;

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>Every Reign in Blood window on the pull, in encounter order, with what landed inside it.</summary>
    public IReadOnlyList<BurstWindow> Windows => Result.Windows;

    /// <summary>Reign in Blood windows the player actually opened on this pull.</summary>
    public int WindowCount => Result.Windows.Count;

    /// <summary>Windows that held Rupture, Bloodbound Spirit and Owed in Blood together.</summary>
    public int FullWindows => Result.FullWindows;

    /// <summary>
    /// Rupture casts that fell outside every Reign in Blood window, counted by containment rather
    /// than by claiming: a second Rupture inside a window an earlier one already satisfied is in a
    /// window and is not counted here.
    /// </summary>
    public int OutOfWindowRuptures => Result.OutOfWindowRuptures;

    /// <summary>
    /// Bloodbound Spirit casts that fell outside every Reign in Blood window, counted by containment
    /// on the same terms as <see cref="OutOfWindowRuptures"/>.
    /// </summary>
    public int OutOfWindowBloodboundSpirits => Result.OutOfWindowBloodboundSpirits;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ReignInBloodSelfBuff))]
    private void OnWindowOpened(ApplyBuffEvent @event)
    {
        if (_open is not null) return;

        _open = new RawWindow(@event.Timestamp);
        _rawWindows.Add(_open);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ReignInBloodSelfBuff))]
    private void OnWindowClosed(RemoveBuffEvent @event)
    {
        if (_open is null) return;

        _open.End = @event.Timestamp;
        _open = null;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Rupture))]
    private void OnRupture(CastEvent @event) => _ruptures.Add(@event.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.BloodboundSpirit))]
    private void OnBloodboundSpirit(CastEvent @event) => _bloodboundSpirits.Add(@event.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.OwedInBlood),
        nameof(Spells.OwedInBloodAoe)])]
    private void OnOwedInBlood(CastEvent @event) => _owedInBloods.Add(@event.Timestamp);

    /// <summary>
    /// Closes any window left open at the pull boundary, then fills the two readings the analyzer
    /// exposes. The per-window presence flags claim casts in encounter order, so the first cast
    /// satisfies a window and no cast satisfies two. The out-of-window counts instead test
    /// containment against every window's span, so a second cast inside an already-satisfied window
    /// is in a window and is never reported as stray. Owed in Blood outside a window is ordinary
    /// filler conversion, so it is not counted as a miss either way.
    /// </summary>
    private Computed Compute()
    {
        var ruptureClaimed = new bool[_ruptures.Count];
        var spiritClaimed = new bool[_bloodboundSpirits.Count];
        var owedClaimed = new bool[_owedInBloods.Count];

        var windows = new List<BurstWindow>(_rawWindows.Count);
        var spans = new List<WindowSpan>(_rawWindows.Count);
        var full = 0;
        foreach (var raw in _rawWindows)
        {
            var end = raw.End ?? Math.Min(raw.Start + BuffDurationMs, Pull.EndTime);
            var window = new BurstWindow(
                raw.Start,
                end,
                Claim(_ruptures, ruptureClaimed, raw.Start, end),
                Claim(_bloodboundSpirits, spiritClaimed, raw.Start, end),
                Claim(_owedInBloods, owedClaimed, raw.Start, end));

            if (window.PresentCount == BurstWindow.CooldownCount) full++;
            windows.Add(window);
            spans.Add(new WindowSpan(raw.Start - LeadInMs, end));
        }

        return new Computed(
            windows,
            full,
            CountOutside(_ruptures, spans),
            CountOutside(_bloodboundSpirits, spans));
    }

    private static bool Claim(List<int> casts, bool[] claimed, int start, int end)
    {
        for (var i = 0; i < casts.Count; i++)
        {
            var timestamp = casts[i];
            if (timestamp > end) break;
            if (claimed[i] || timestamp < start - LeadInMs) continue;

            claimed[i] = true;
            return true;
        }

        return false;
    }

    private static int CountOutside(List<int> casts, List<WindowSpan> spans) =>
        casts.Count(timestamp => !spans.Any(span => timestamp >= span.Start && timestamp <= span.End));

    private sealed class RawWindow(int start)
    {
        public int Start { get; } = start;
        public int? End { get; set; }
    }

    private readonly record struct WindowSpan(int Start, int End);

    private sealed record Computed(
        IReadOnlyList<BurstWindow> Windows,
        int FullWindows,
        int OutOfWindowRuptures,
        int OutOfWindowBloodboundSpirits);

    /// <summary>
    /// One Reign in Blood amplifier window in absolute timestamps, and which of the three burst
    /// cooldowns were deployed into it.
    /// </summary>
    public sealed record BurstWindow(int Start, int End, bool RuptureIn, bool BloodboundSpiritIn, bool OwedInBloodIn)
    {
        /// <summary>Burst cooldowns a window can hold.</summary>
        public const int CooldownCount = 3;

        /// <summary>How many of the three burst cooldowns landed in this window.</summary>
        public int PresentCount => (RuptureIn ? 1 : 0) + (BloodboundSpiritIn ? 1 : 0) + (OwedInBloodIn ? 1 : 0);
    }
}

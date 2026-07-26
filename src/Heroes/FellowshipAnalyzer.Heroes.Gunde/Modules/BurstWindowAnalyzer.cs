using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Measures how tightly Gunde stacked his three burst cooldowns into the same window. Reign in Blood
/// is the amplifier the whole burst is built around: for 12 seconds an additional 30% of ability
/// damage converts into Rend, and it only comes back every 90 seconds. Bloodbound Spirit (a 20s
/// self-buff worth 15% on Heart Splitter and Grim Carve, plus haste) and Rupture (the single biggest
/// hit, and the Open Wounds application) should both land inside that window so their damage is
/// converted at the boosted rate, and held Blood Feather stacks should be deployed there through
/// Owed in Blood. Spreading the three across the fight instead is the failure this analyzer names.
/// </summary>
/// <remarks>
/// Windows are anchored on the Reign in Blood self-buff rather than on its cast, so a window reflects
/// the amplifier actually being live. Bloodbound Spirit is commonly pre-cast and Reign in Blood
/// triggered on top of it, so a cast up to <see cref="LeadInMs"/> before the buff lands still belongs
/// to the window. Each cast is claimed by at most one window, earliest window first, so a single
/// Rupture can never satisfy two windows. Assignment runs once when the pull's results are first read.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class BurstWindowAnalyzer : Analyzer
{
    /// <summary>Data-verified duration of the Reign in Blood self-buff, used only when no removal was logged.</summary>
    public const int BuffDurationMs = 12_000;

    /// <summary>Grace period before a window's start in which a cast still counts as part of the burst.</summary>
    public const int LeadInMs = 3_000;

    private const int FallbackCooldownMs = 90_000;

    /// <summary>
    /// Reign in Blood's base cooldown in milliseconds, taken from the spell registry and unaffected
    /// by haste. Exposed so the guide reports the same number the window budget is derived from.
    /// </summary>
    public static readonly int ReignInBloodCooldownMs =
        Spells.ReignInBlood.Cooldown is { } cooldown && cooldown > 0
            ? (int)(cooldown * 1000)
            : FallbackCooldownMs;

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

    /// <summary>Rupture casts that landed outside every Reign in Blood window.</summary>
    public int OutOfWindowRuptures => Result.OutOfWindowRuptures;

    /// <summary>Bloodbound Spirit casts that landed outside every Reign in Blood window.</summary>
    public int OutOfWindowBloodboundSpirits => Result.OutOfWindowBloodboundSpirits;

    /// <summary>
    /// Reign in Blood windows the pull was long enough to allow, counting the opener plus one per
    /// full cooldown that elapsed. The cooldown is read from the spell registry (90 seconds, and
    /// unhasted - Reign in Blood does not scale with haste in the source data). Never reports fewer
    /// than <see cref="WindowCount"/>, so a pull that somehow held more windows than the cooldown
    /// permits still reads as complete rather than as an impossible overshoot.
    /// </summary>
    public int PossibleWindows => Result.PossibleWindows;

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

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.OwedInBlood))]
    private void OnOwedInBlood(CastEvent @event) => _owedInBloods.Add(@event.Timestamp);

    /// <summary>
    /// Closes any window left open at the pull boundary, then claims casts for windows in encounter
    /// order. A single pass fills the per-window flags and the out-of-window counts together, so the
    /// two readings can never disagree about which cast belonged where. Owed in Blood outside a
    /// window is ordinary filler conversion, so it is not counted as a miss.
    /// </summary>
    private Computed Compute()
    {
        var ruptureClaimed = new bool[_ruptures.Count];
        var spiritClaimed = new bool[_bloodboundSpirits.Count];
        var owedClaimed = new bool[_owedInBloods.Count];

        var windows = new List<BurstWindow>(_rawWindows.Count);
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
        }

        var duration = Pull.EndTime - Pull.StartTime;
        var allowed = 1 + (Math.Max(0, duration - 1) / ReignInBloodCooldownMs);

        return new Computed(
            windows,
            full,
            ruptureClaimed.Count(claimed => !claimed),
            spiritClaimed.Count(claimed => !claimed),
            Math.Max(windows.Count, allowed));
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

    private sealed class RawWindow(int start)
    {
        public int Start { get; } = start;
        public int? End { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<BurstWindow> Windows,
        int FullWindows,
        int OutOfWindowRuptures,
        int OutOfWindowBloodboundSpirits,
        int PossibleWindows);

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

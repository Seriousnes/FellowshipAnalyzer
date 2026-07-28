using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures one defensive's active windows and the incoming damage they covered. The analyzer holds
/// the window machinery; a per-hero leaf supplies <see cref="DefensiveSpellId"/> and declares the
/// <c>[On&lt;&gt;]</c> handlers, because the generator inlines each subscription's spell predicate at
/// the declaring type.
/// <para>
/// Apply and refresh handlers call <see cref="OpenWindow"/>, remove handlers call
/// <see cref="CloseWindow"/>, and a <c>[On&lt;DamageEvent&gt;(To = Actor.Player)]</c> handler calls
/// <see cref="RecordDamageTaken"/> for every hit the player takes; only hits landing inside an open
/// window are attributed. A window still open when the pull ends closes at <see cref="Analyzer.Pull"/>'s
/// end time and is flagged <see cref="DefensiveWindow.Truncated"/>.
/// </para>
/// <para>
/// Nothing accumulates unless the hero's spellbook flags <see cref="DefensiveSpellId"/> with
/// <see cref="SpellbookAbility.IsDefensive"/>, so the set of tracked defensives is the spellbook's to
/// declare rather than a list held here.
/// </para>
/// <para>
/// <see cref="DamagePrevented"/> is <see cref="DamageEvent.Mitigated"/> plus
/// <see cref="DamageEvent.Absorbed"/>. <see cref="DamageEvent.Blocked"/> is a labelled portion of
/// <see cref="DamageEvent.Mitigated"/>, not a third bucket - on Fellowship block events
/// <c>UnmitigatedAmount - Amount</c> equals <c>Mitigated + Absorbed</c> exactly - so it is reported
/// separately as <see cref="DamageBlocked"/> and never added to the prevented total.
/// </para>
/// </summary>
public abstract class MajorDefensiveAnalyzer : Analyzer
{
    private readonly List<WindowCapture> _windows = [];

    private WindowCapture? _open;

    private Computed Result => field ??= Compute();

    private Abilities Abilities => field ??= Owner.GetModule<Abilities>()!;

    /// <summary>The spellbook id of the defensive this analyzer measures.</summary>
    protected abstract int DefensiveSpellId { get; }

    /// <summary>The spellbook entry for <see cref="DefensiveSpellId"/>, or <c>null</c> when the hero does not declare it.</summary>
    public SpellbookAbility? Ability => Abilities.GetAbility(DefensiveSpellId);

    /// <summary>
    /// Whether the hero's spellbook flags <see cref="DefensiveSpellId"/> as a defensive. When false the
    /// analyzer records nothing, so every metric below reads zero.
    /// </summary>
    public bool TracksDefensive => Ability?.IsDefensive == true;

    /// <summary>Every window the defensive was active for, in encounter order.</summary>
    public IReadOnlyList<DefensiveWindow> Windows => Result.Windows;

    /// <summary>How many times the defensive went active this pull.</summary>
    public int WindowCount => Result.Windows.Count;

    /// <summary>Total milliseconds the defensive was active this pull.</summary>
    public int ActiveMs => Result.ActiveMs;

    /// <summary>Share of the pull (0-1) the defensive was active for.</summary>
    public double Uptime
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)ActiveMs / duration, 0, 1) : 0;
        }
    }

    /// <summary>Damage the player would have taken inside the windows but did not, from mitigation and absorbs.</summary>
    public long DamagePrevented => Result.Mitigated + Result.Absorbed;

    /// <summary>The mitigated portion of <see cref="DamagePrevented"/>.</summary>
    public long DamageMitigated => Result.Mitigated;

    /// <summary>The absorbed portion of <see cref="DamagePrevented"/>.</summary>
    public long DamageAbsorbed => Result.Absorbed;

    /// <summary>The share of <see cref="DamageMitigated"/> the log attributes to blocks.</summary>
    public long DamageBlocked => Result.Blocked;

    /// <summary>Damage that still landed on the player inside the windows.</summary>
    public long DamageTaken => Result.Taken;

    /// <summary>The raw incoming damage the windows faced, before any mitigation.</summary>
    public long DamageFaced => Result.Unmitigated;

    /// <summary>Hits the player took inside the windows.</summary>
    public int HitsCovered => Result.Hits;

    /// <summary>Hits inside the windows that came in as a block.</summary>
    public int BlockedHits => Result.BlockedHits;

    /// <summary>Share (0-1) of <see cref="DamageFaced"/> the windows prevented.</summary>
    public double PreventedShare =>
        Result.Unmitigated > 0 ? Math.Clamp(DamagePrevented / (double)Result.Unmitigated, 0, 1) : 0;

    /// <summary>
    /// Opens a window at <paramref name="timestamp"/> unless one is already open. Call from apply and
    /// refresh handlers; a refresh that finds a window already open extends it rather than splitting it.
    /// </summary>
    protected void OpenWindow(int timestamp)
    {
        if (!TracksDefensive || _open is not null) return;

        _open = new WindowCapture { Start = timestamp, End = timestamp };
        _windows.Add(_open);
    }

    /// <summary>Closes the window open at <paramref name="timestamp"/>, if any. Call from remove handlers.</summary>
    protected void CloseWindow(int timestamp)
    {
        if (_open is null) return;

        _open.End = Math.Max(_open.Start, timestamp);
        _open = null;
    }

    /// <summary>
    /// Attributes <paramref name="damageEvent"/> to the open window, if there is one. Call from a
    /// <c>[On&lt;DamageEvent&gt;(To = Actor.Player)]</c> handler on the leaf.
    /// </summary>
    protected void RecordDamageTaken(DamageEvent damageEvent)
    {
        if (_open is null) return;

        _open.End = Math.Max(_open.End, damageEvent.Timestamp);
        _open.Hits++;
        _open.Taken += damageEvent.Amount;
        _open.Unmitigated += damageEvent.UnmitigatedAmount;
        _open.Mitigated += damageEvent.Mitigated;
        _open.Absorbed += damageEvent.Absorbed ?? 0;
        _open.Blocked += damageEvent.Blocked;

        if (damageEvent.HitType == HitType.Block) _open.BlockedHits++;
    }

    private Computed Compute()
    {
        var windows = new List<DefensiveWindow>(_windows.Count);
        var activeMs = 0;
        long taken = 0, unmitigated = 0, mitigated = 0, absorbed = 0, blocked = 0;
        var hits = 0;
        var blockedHits = 0;

        foreach (var capture in _windows)
        {
            var truncated = ReferenceEquals(capture, _open);
            var end = truncated ? Math.Max(capture.End, Pull.EndTime) : capture.End;

            windows.Add(new DefensiveWindow(
                capture.Start,
                end,
                capture.Taken,
                capture.Unmitigated,
                capture.Mitigated,
                capture.Absorbed,
                capture.Blocked,
                capture.Hits,
                capture.BlockedHits,
                truncated));

            activeMs += end - capture.Start;
            taken += capture.Taken;
            unmitigated += capture.Unmitigated;
            mitigated += capture.Mitigated;
            absorbed += capture.Absorbed;
            blocked += capture.Blocked;
            hits += capture.Hits;
            blockedHits += capture.BlockedHits;
        }

        return new Computed(windows, activeMs, taken, unmitigated, mitigated, absorbed, blocked, hits, blockedHits);
    }

    private sealed class WindowCapture
    {
        public int Start { get; init; }
        public int End { get; set; }
        public long Taken { get; set; }
        public long Unmitigated { get; set; }
        public long Mitigated { get; set; }
        public long Absorbed { get; set; }
        public long Blocked { get; set; }
        public int Hits { get; set; }
        public int BlockedHits { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<DefensiveWindow> Windows,
        int ActiveMs,
        long Taken,
        long Unmitigated,
        long Mitigated,
        long Absorbed,
        long Blocked,
        int Hits,
        int BlockedHits);
}

/// <summary>
/// One window a defensive was active for, and the incoming damage that landed inside it.
/// </summary>
/// <param name="Start">When the defensive went active.</param>
/// <param name="End">When it fell off, or the pull's end time when <paramref name="Truncated"/>.</param>
/// <param name="Taken">Damage that still landed on the player.</param>
/// <param name="Unmitigated">The raw incoming damage before any mitigation.</param>
/// <param name="Mitigated">The portion prevented by damage reduction, blocks included.</param>
/// <param name="Absorbed">The portion prevented by absorb shields.</param>
/// <param name="Blocked">The share of <paramref name="Mitigated"/> the log attributes to blocks.</param>
/// <param name="Hits">Hits the player took inside the window.</param>
/// <param name="BlockedHits">How many of <paramref name="Hits"/> came in as a block.</param>
/// <param name="Truncated">Whether the pull ended before the defensive fell off.</param>
public sealed record DefensiveWindow(
    int Start,
    int End,
    long Taken,
    long Unmitigated,
    long Mitigated,
    long Absorbed,
    long Blocked,
    int Hits,
    int BlockedHits,
    bool Truncated)
{
    /// <summary>How long the defensive was active, in milliseconds.</summary>
    public int DurationMs => End - Start;

    /// <summary>Damage the window prevented, from mitigation and absorbs.</summary>
    public long Prevented => Mitigated + Absorbed;
}

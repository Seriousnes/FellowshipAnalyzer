using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>One continuous stretch of a debuff on an enemy, in absolute timestamps.</summary>
public sealed record MaraDotWindow(int Start, int End);

/// <summary>
/// How continuously one maintained effect stood on a pull's primary target: the windows themselves,
/// the share of the pull they cover, and the uncovered stretches between them.
/// </summary>
public sealed record MaraDotUptime(
    MaraDot Dot,
    IReadOnlyList<MaraDotWindow> Windows,
    double Uptime,
    int GapCount,
    int TotalGapMs);

/// <summary>
/// One Hemorrhage application, carrying the combo points the preceding Hemorrhaging Strike spent and
/// the bleed duration those points bought.
/// </summary>
public sealed record HemorrhageApplication(int Timestamp, int ComboPoints, int ExpectedDurationMs, bool Refresh);

/// <summary>
/// One Hemorrhage reapplication, with how much of the standing bleed it overwrote. A large
/// <paramref name="RemainingMs"/> is a bleed clipped well before it would have finished ticking.
/// </summary>
public sealed record HemorrhageRefresh(int Timestamp, int RemainingMs);

/// <summary>
/// Measures how continuously Mara kept her maintained poisons and bleeds rolling on a boss pull.
/// Windows are tracked per effect and per (TargetId, TargetInstance) from apply, refresh and remove
/// events, with a still-open window closed at pull end; each effect's primary target is the one
/// carrying the most debuff time, so transient adds never dilute the boss reading. Uptime is that
/// target's covered time against the pull duration, and gaps are the uncovered stretches between its
/// windows, so the lead-in before the first application lowers uptime without counting as a gap.
/// <para>
/// Hemorrhage is measured further, because its duration is bought rather than fixed: the bleed lasts
/// <see cref="HemorrhageBaseDurationMs"/> plus <see cref="HemorrhageComboPointDurationMs"/> per combo
/// point spent on the Hemorrhaging Strike that laid it, read from that cast's combo-point snapshot.
/// Every reapplication is recorded with the time still left on the window it overwrote, so the guide
/// can decide how much remaining time counts as clipping the bleed early.
/// </para>
/// </summary>
[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class MaraDotUptimeAnalyzer : Analyzer, IMaraDotAnalyzer
{
    /// <summary>The Hemorrhage bleed's duration before any combo points are spent, in milliseconds.</summary>
    public const int HemorrhageBaseDurationMs = 12_000;

    /// <summary>Milliseconds each combo point spent on Hemorrhaging Strike adds to the bleed.</summary>
    public const int HemorrhageComboPointDurationMs = 3_000;

    private readonly Dictionary<(int Slot, int TargetId, int TargetInstance), List<MaraDotWindow>> _windows = [];
    private readonly Dictionary<(int Slot, int TargetId, int TargetInstance), int> _openWindows = [];
    private readonly Dictionary<(int TargetId, int TargetInstance), int> _hemorrhageExpiry = [];
    private readonly List<HemorrhageApplication> _hemorrhageApplications = [];
    private readonly List<HemorrhageRefresh> _hemorrhageRefreshes = [];

    private int _comboPointsOnStrike;

    private IReadOnlyList<MaraDotUptime>? _dots;

    /// <summary>The maintained effects' readings, in <see cref="MaraDots.Maintained"/> order.</summary>
    public IReadOnlyList<MaraDotUptime> Dots => _dots ??= Compute();

    /// <summary>How continuously the Seething Poison stood on the boss.</summary>
    public MaraDotUptime SeethingPoison => Find(MaraDots.SeethingPoison);

    /// <summary>How continuously the Hemorrhage bleed stood on the boss.</summary>
    public MaraDotUptime Hemorrhage => Find(MaraDots.Hemorrhage);

    /// <summary>Every Hemorrhage application and reapplication, in encounter order.</summary>
    public IReadOnlyList<HemorrhageApplication> HemorrhageApplications => _hemorrhageApplications;

    /// <summary>Every Hemorrhage reapplication, with the time left on the window it overwrote.</summary>
    public IReadOnlyList<HemorrhageRefresh> HemorrhageRefreshes => _hemorrhageRefreshes;

    /// <summary>Pull length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HemorrhagingStrike))]
    private void OnHemorrhagingStrike(CastEvent e)
    {
        var resources = e.SourceResources?.Resources;
        if (resources is null) return;

        foreach (var resource in resources)
        {
            if (resource.Type != ResourceTypes.Secondary) continue;

            _comboPointsOnStrike = resource.Amount;
            return;
        }
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnApplied(ApplyDebuffEvent e) => OpenWindow(e, refresh: false);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnRefreshed(RefreshDebuffEvent e) => OpenWindow(e, refresh: true);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnRemoved(RemoveDebuffEvent e)
    {
        var slot = Slot(e.Ability.Id);
        if (slot < 0) return;

        var key = (slot, e.TargetId, e.TargetInstance ?? 0);
        if (_openWindows.Remove(key, out var start))
            WindowsFor(key).Add(new MaraDotWindow(start, e.Timestamp));

        if (e.Ability.Id == MaraDots.Hemorrhage.EffectId)
            _hemorrhageExpiry.Remove((e.TargetId, e.TargetInstance ?? 0));
    }

    /// <summary>
    /// Opens a window for the effect on the target when none is standing, then stamps Hemorrhage's
    /// bought duration. The reapplication is measured against the previous application's expected
    /// expiry before that expiry is replaced, so the recorded remaining time belongs to the window
    /// being overwritten.
    /// </summary>
    private void OpenWindow(BuffEvent e, bool refresh)
    {
        var slot = Slot(e.Ability.Id);
        if (slot < 0) return;

        _openWindows.TryAdd((slot, e.TargetId, e.TargetInstance ?? 0), e.Timestamp);

        if (e.Ability.Id != MaraDots.Hemorrhage.EffectId) return;

        var target = (e.TargetId, e.TargetInstance ?? 0);
        if (refresh)
        {
            var remaining = _hemorrhageExpiry.TryGetValue(target, out var expiry)
                ? Math.Max(0, expiry - e.Timestamp)
                : 0;
            _hemorrhageRefreshes.Add(new HemorrhageRefresh(e.Timestamp, remaining));
        }

        var duration = HemorrhageBaseDurationMs + (HemorrhageComboPointDurationMs * _comboPointsOnStrike);
        _hemorrhageApplications.Add(new HemorrhageApplication(e.Timestamp, _comboPointsOnStrike, duration, refresh));
        _hemorrhageExpiry[target] = e.Timestamp + duration;
    }

    private List<MaraDotWindow> WindowsFor((int Slot, int TargetId, int TargetInstance) key)
    {
        if (_windows.TryGetValue(key, out var windows)) return windows;

        windows = [];
        _windows[key] = windows;
        return windows;
    }

    /// <summary>
    /// Closes still-open windows at the pull boundary and picks each effect's primary target. A window
    /// still open at pull end is a target that died without a logged remove; the boss, the max-coverage
    /// target, dies at the pull's end time. Computed once, on first read.
    /// </summary>
    private List<MaraDotUptime> Compute()
    {
        var windows = new Dictionary<(int Slot, int TargetId, int TargetInstance), List<MaraDotWindow>>();
        foreach (var (key, list) in _windows)
            windows[key] = [.. list];
        foreach (var (key, start) in _openWindows)
        {
            if (!windows.TryGetValue(key, out var list))
                windows[key] = list = [];
            list.Add(new MaraDotWindow(start, Pull.EndTime));
        }

        var duration = Pull.EndTime - Pull.StartTime;
        var results = new List<MaraDotUptime>(MaraDots.Maintained.Count);

        for (var slot = 0; slot < MaraDots.Maintained.Count; slot++)
        {
            List<MaraDotWindow>? primary = null;
            var primaryCovered = 0;
            foreach (var (key, list) in windows)
            {
                if (key.Slot != slot) continue;

                var covered = list.Sum(window => window.End - window.Start);
                if (covered <= primaryCovered) continue;

                primary = list;
                primaryCovered = covered;
            }

            if (primary is null)
            {
                results.Add(new MaraDotUptime(MaraDots.Maintained[slot], [], 0d, 0, 0));
                continue;
            }

            var gapCount = 0;
            var totalGapMs = 0;
            for (var i = 1; i < primary.Count; i++)
            {
                var gap = primary[i].Start - primary[i - 1].End;
                if (gap <= 0) continue;

                gapCount++;
                totalGapMs += gap;
            }

            var uptime = duration > 0 ? Math.Min(1d, primaryCovered / (double)duration) : 0d;
            results.Add(new MaraDotUptime(MaraDots.Maintained[slot], primary, uptime, gapCount, totalGapMs));
        }

        return results;
    }

    private MaraDotUptime Find(MaraDot dot)
    {
        foreach (var entry in Dots)
        {
            if (entry.Dot.EffectId == dot.EffectId) return entry;
        }

        throw new InvalidOperationException($"{dot.Name} is not a maintained Mara effect.");
    }

    private static int Slot(int abilityId)
    {
        for (var i = 0; i < MaraDots.Maintained.Count; i++)
        {
            if (MaraDots.Maintained[i].EffectId == abilityId) return i;
        }
        return -1;
    }
}

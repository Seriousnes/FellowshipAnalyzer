using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures Tariq's empowerment windows on every pull, regardless of shape. Thunder Call and Raging
/// Tempest each apply a self-buff that empowers the Fury spenders and is the only state in which
/// Chain Lightning is castable, so the windows are where a pull's damage is concentrated: Fury should
/// already be banked when one opens, and the spenders should be dumped before it closes.
/// <para>
/// Both buffs are read directly from the player's own apply/remove stream rather than from their casts,
/// because the buff is the empowerment and its logged duration is what the spenders actually get. A
/// window still open when the pull ends is closed at <see cref="Analyzer.Pull"/>'s end time and flagged
/// <see cref="ThunderCallWindow.ClippedByPullEnd"/>, which is the signal for a cooldown pressed into a
/// dying pull.
/// </para>
/// </summary>
/// <remarks>
/// Casts are attributed to the most recently opened window that is still open. The two buffs run on
/// separate cooldowns and are not meant to be stacked, so nesting is rare; when it does happen the
/// newer window is the one the player opened deliberately and the overlap is surfaced on both windows
/// through <see cref="ThunderCallWindow.OverlapsOtherWindow"/>, since overlapping the two wastes the
/// empowerment of whichever is already running.
/// <para>
/// A second apply of a buff whose window is already open is a no-op rather than a new window, and a
/// remove with no matching open window (a buff carried in from before the pull) is ignored rather than
/// back-filled to the pull start.
/// </para>
/// <para>
/// Fury is read as a percentage of the logged pool rather than in raw units, because the logged maximum
/// is stat-scaled and grows across a dungeon run; the reading is derived the same way
/// <see cref="FuryTracker"/> derives it so the surfaces agree on a given cast. The running value is
/// pull-local, so <see cref="ThunderCallWindow.FuryAtOpen"/> is null for a window that opens before any
/// cast in the pull carried a snapshot.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class ThunderCallWindowAnalyzer : Analyzer
{
    /// <summary>
    /// Milliseconds both empowerment buffs are meant to last. Season 3 <c>hero_data</c> Kit constants
    /// (Thunder Call Duration 21, Raging Tempest BuffDuration 21).
    /// </summary>
    public const int ExpectedWindowDurationMs = 21_000;

    private const int FuryCap = 100;

    private readonly List<OpenWindow> _windows = [];
    private readonly List<OpenWindow> _open = [];

    private int? _furyPercent;

    private List<ThunderCallWindow>? _evaluated;
    private List<ThunderCallWindow> Evaluated => _evaluated ??= Build();

    /// <summary>Every empowerment window in the pull, in the order they opened.</summary>
    public IReadOnlyList<ThunderCallWindow> Windows => Evaluated;

    public int WindowCount => Evaluated.Count;

    /// <summary>Windows the pull ended inside, so the empowerment was cut short.</summary>
    public int ClippedWindows => Evaluated.Count(window => window.ClippedByPullEnd);

    /// <summary>Windows that ran at the same time as a window of the other kind.</summary>
    public int OverlappingWindows => Evaluated.Count(window => window.OverlapsOtherWindow);

    /// <summary>
    /// Mean Fury held as a window opened, over the windows that had a reading. Null when no window in
    /// the pull opened after an observed Fury snapshot.
    /// </summary>
    public double? AverageFuryAtOpen
    {
        get
        {
            var readings = Evaluated.Where(window => window.FuryAtOpen is not null).ToList();
            return readings.Count == 0 ? null : readings.Average(window => window.FuryAtOpen!.Value);
        }
    }

    public int TotalSpenderCasts => Evaluated.Sum(window => window.SpenderCasts);

    public int TotalChainLightningCasts => Evaluated.Sum(window => window.ChainLightningCasts);

    public double AverageSpendersPerWindow =>
        Evaluated.Count == 0 ? 0d : (double)TotalSpenderCasts / Evaluated.Count;

    [On<ApplyBuffEvent>(To = Actor.Player, Spells = new[]
    {
        nameof(Spells.ThunderCallBuff),
        nameof(Spells.RagingTempestPulsatingSingleDamageSelfBuff),
    })]
    private void OnWindowOpened(ApplyBuffEvent @event)
    {
        var source = SourceOf(@event.Ability.Id);
        foreach (var open in _open)
        {
            if (open.Source == source)
                return;
        }

        var window = new OpenWindow(source, @event.Timestamp, _furyPercent);
        _windows.Add(window);
        _open.Add(window);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spells = new[]
    {
        nameof(Spells.ThunderCallBuff),
        nameof(Spells.RagingTempestPulsatingSingleDamageSelfBuff),
    })]
    private void OnWindowClosed(RemoveBuffEvent @event)
    {
        var source = SourceOf(@event.Ability.Id);
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            if (_open[i].Source != source)
                continue;

            _open[i].ClosedAt = @event.Timestamp;
            _open.RemoveAt(i);
            return;
        }
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        if (FuryPercent(@event) is { } fury)
            _furyPercent = fury;

        if (_open.Count > 0)
            _open[^1].Casts.Add(@event);
    }

    private List<ThunderCallWindow> Build()
    {
        var count = _windows.Count;
        var closes = new int[count];
        var clipped = new bool[count];
        for (var i = 0; i < count; i++)
        {
            clipped[i] = _windows[i].ClosedAt is null;
            closes[i] = _windows[i].ClosedAt ?? Pull.EndTime;
        }

        var overlaps = new bool[count];
        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                if (_windows[i].Source == _windows[j].Source)
                    continue;

                if (_windows[i].OpenedAt >= closes[j] || _windows[j].OpenedAt >= closes[i])
                    continue;

                overlaps[i] = true;
                overlaps[j] = true;
            }
        }

        var built = new List<ThunderCallWindow>(count);
        for (var i = 0; i < count; i++)
        {
            var window = _windows[i];
            built.Add(new ThunderCallWindow
            {
                Source = window.Source,
                OpenedAt = window.OpenedAt,
                ClosedAt = closes[i],
                ClippedByPullEnd = clipped[i],
                FuryAtOpen = window.FuryAtOpen,
                SkullCrusherCasts = CountCasts(window.Casts, Spells.SkullCrusher.FSLID),
                HammerStormCasts = CountCasts(window.Casts, Spells.HammerStorm.FSLID),
                CullingStrikeCasts = CountCasts(window.Casts, Spells.CullingStrike.FSLID),
                ChainLightningCasts = CountCasts(window.Casts, Spells.ChainLightning.FSLID),
                CastsInWindow = window.Casts,
                OverlapsOtherWindow = overlaps[i],
            });
        }

        return built;
    }

    private static int CountCasts(List<CastEvent> casts, int spellId)
    {
        var count = 0;
        foreach (var cast in casts)
        {
            if (cast.Ability.Id == spellId)
                count++;
        }

        return count;
    }

    private static ThunderCallWindowSource SourceOf(int abilityId) =>
        abilityId == Spells.ThunderCallBuff.FSLID
            ? ThunderCallWindowSource.ThunderCall
            : ThunderCallWindowSource.RagingTempest;

    private static int? FuryPercent(Event @event)
    {
        var resources = @event.SourceResources?.Resources;
        if (resources is null)
            return null;

        foreach (var resource in resources)
        {
            if (resource.Type != ResourceTypes.Primary)
                continue;

            return resource.Max > 0
                ? (int)Math.Clamp(Math.Round(resource.Amount * 100.0 / resource.Max), 0, FuryCap)
                : null;
        }

        return null;
    }

    private sealed class OpenWindow(ThunderCallWindowSource source, int openedAt, int? furyAtOpen)
    {
        public ThunderCallWindowSource Source { get; } = source;
        public int OpenedAt { get; } = openedAt;
        public int? FuryAtOpen { get; } = furyAtOpen;
        public int? ClosedAt { get; set; }
        public List<CastEvent> Casts { get; } = [];
    }
}

/// <summary>Which cooldown's self-buff opened an empowerment window.</summary>
public enum ThunderCallWindowSource
{
    ThunderCall,
    RagingTempest,
}

/// <summary>
/// One empowerment window: the buff that opened it, the Fury banked as it opened, and the spenders and
/// Chain Lightning casts that landed inside it.
/// </summary>
public sealed record ThunderCallWindow
{
    public required ThunderCallWindowSource Source { get; init; }

    public required int OpenedAt { get; init; }

    /// <summary>
    /// When the buff fell off, or the pull's end time when it was still up as the pull ended (see
    /// <see cref="ClippedByPullEnd"/>).
    /// </summary>
    public required int ClosedAt { get; init; }

    public int DurationMs => ClosedAt - OpenedAt;

    /// <summary>Whether the pull ended with the buff still up, cutting the window short.</summary>
    public required bool ClippedByPullEnd { get; init; }

    /// <summary>
    /// Fury held (0-100) at the last cast snapshot before the window opened, or null when no cast in the
    /// pull had carried a snapshot yet.
    /// </summary>
    public int? FuryAtOpen { get; init; }

    public int SkullCrusherCasts { get; init; }
    public int HammerStormCasts { get; init; }
    public int CullingStrikeCasts { get; init; }
    public int ChainLightningCasts { get; init; }

    public int SpenderCasts => SkullCrusherCasts + HammerStormCasts + CullingStrikeCasts;

    /// <summary>Every player cast attributed to this window, in dispatch order.</summary>
    public IReadOnlyList<CastEvent> CastsInWindow { get; init; } = [];

    /// <summary>Whether a window of the other kind was running at the same time.</summary>
    public bool OverlapsOtherWindow { get; init; }
}

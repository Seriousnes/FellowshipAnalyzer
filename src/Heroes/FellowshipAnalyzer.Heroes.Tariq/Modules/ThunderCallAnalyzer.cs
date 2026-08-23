using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class ThunderCallAnalyzer : Analyzer
{
    public const int ThunderCallWindowDurationMs = 21_000;

    public const int RagingTempestWindowDurationMs = 20_000;

    private const int FuryCap = 100;

    public static int ExpectedDurationMs(ThunderCallWindowSource source) =>
        source == ThunderCallWindowSource.ThunderCall
            ? ThunderCallWindowDurationMs
            : RagingTempestWindowDurationMs;

    private readonly List<OpenWindow> _windows = [];
    private readonly List<OpenWindow> _open = [];

    private int? _furyPercent;

    private List<ThunderCallWindow> Evaluated => field ??= Build();

    public List<ThunderCallWindow> Windows => Evaluated;

    public int WindowCount => Evaluated.Count;

    public int ClippedWindows => Evaluated.Count(window => window.ClippedByPullEnd);

    public int OverlappingWindows => Evaluated.Count(window => window.OverlapsOtherWindow);

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

public enum ThunderCallWindowSource
{
    ThunderCall,
    RagingTempest,
}

public sealed record ThunderCallWindow
{
    public required ThunderCallWindowSource Source { get; init; }

    public required int OpenedAt { get; init; }

    public required int ClosedAt { get; init; }

    public int DurationMs => ClosedAt - OpenedAt;

    public required bool ClippedByPullEnd { get; init; }

    public int? FuryAtOpen { get; init; }

    public int SkullCrusherCasts { get; init; }
    public int HammerStormCasts { get; init; }
    public int CullingStrikeCasts { get; init; }
    public int ChainLightningCasts { get; init; }

    public int SpenderCasts => SkullCrusherCasts + HammerStormCasts + CullingStrikeCasts;

    public List<CastEvent> CastsInWindow { get; init; } = [];

    public bool OverlapsOtherWindow { get; init; }

    public int ExpectedDurationMs => ThunderCallAnalyzer.ExpectedDurationMs(Source);
}

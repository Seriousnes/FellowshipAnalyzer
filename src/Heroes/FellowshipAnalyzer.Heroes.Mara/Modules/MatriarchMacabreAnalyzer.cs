using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record MatriarchMacabreWindow
{
    public required int OpenedAt { get; init; }

    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    public int QueensFangCasts { get; init; }

    public int ArachnidAssaultCasts { get; init; }

    public int FinisherCasts => QueensFangCasts + ArachnidAssaultCasts;

    public bool Converted => FinisherCasts > 0;
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MatriarchMacabreAnalyzer : Analyzer
{
    public const int WindowMs = 20_000;

    public const double DamageIncrease = 0.20;

    private readonly List<MacabreSpan> _spans = [];

    private MacabreSpan? _openSpan;

    public List<MatriarchMacabreWindow> Windows => field ??= Build();

    public int WindowCount => Windows.Count;

    public int MatriarchMacabreCasts { get; private set; }

    public int ConvertedWindows => Windows.Count(window => window.Converted);

    public int FinisherCasts => Windows.Sum(window => window.FinisherCasts);

    public int QueensFangCasts => Windows.Sum(window => window.QueensFangCasts);

    public int ArachnidAssaultCasts => Windows.Sum(window => window.ArachnidAssaultCasts);

    public double AverageFinisherCasts =>
        Windows.Count == 0 ? 0d : (double)FinisherCasts / Windows.Count;

    public long AddedDamage { get; private set; }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamageDealt(DamageEvent damageEvent)
    {
        if (_openSpan is null) return;

        AddedDamage += CombatMath.CalculateEffectiveDamage(damageEvent, DamageIncrease);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.MatriarchMacabre))]
    private void OnMatriarchCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        MatriarchMacabreCasts++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MatriarchMacabreSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent buffEvent)
    {
        if (_openSpan is not null)
            return;

        _openSpan = new MacabreSpan(buffEvent.Timestamp);
        _spans.Add(_openSpan);
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MatriarchMacabreSelfBuff))]
    private void OnBuffRefreshed(RefreshBuffEvent buffEvent)
    {
        if (_openSpan is not null)
            return;

        _openSpan = new MacabreSpan(buffEvent.Timestamp);
        _spans.Add(_openSpan);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MatriarchMacabreSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent)
    {
        if (_openSpan is null)
            return;

        _openSpan.ClosedAt = Math.Max(_openSpan.OpenedAt, buffEvent.Timestamp);
        _openSpan = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.QueenFang), nameof(Spells.ArachnidAssault)])]
    private void OnFinisherCast(CastEvent castEvent)
    {
        if (castEvent.Fake || _openSpan is not { } span)
            return;

        if (castEvent.Ability.Id == Spells.QueenFang.Id)
            span.QueensFangCasts++;
        else
            span.ArachnidAssaultCasts++;
    }

    private List<MatriarchMacabreWindow> Build()
    {
        var windows = new List<MatriarchMacabreWindow>(_spans.Count);
        foreach (var span in _spans)
        {
            windows.Add(new MatriarchMacabreWindow
            {
                OpenedAt = span.OpenedAt,
                ClosedAt = span.ClosedAt ?? Math.Max(span.OpenedAt, Pull.EndTime),
                QueensFangCasts = span.QueensFangCasts,
                ArachnidAssaultCasts = span.ArachnidAssaultCasts,
            });
        }
        return windows;
    }

    private sealed class MacabreSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int QueensFangCasts { get; set; }
        public int ArachnidAssaultCasts { get; set; }
    }
}

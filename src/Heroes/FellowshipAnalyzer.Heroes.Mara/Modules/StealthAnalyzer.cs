using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record StealthWindow
{
    public required int OpenedAt { get; init; }
    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    public int? ConvertingAbilityId { get; init; }

    public int? ConvertingCastAt { get; init; }

    public int? PoisonEffectId { get; init; }

    public bool Converted => ConvertingAbilityId is not null;
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class StealthAnalyzer : Analyzer
{
    public const int PoisonLinkWindowMs = 1000;

    private readonly List<StealthSpan> _spans = [];

    private StealthSpan? _openSpan;
    private StealthSpan? _lastConverted;

    public IReadOnlyList<StealthWindow> Windows => field ??= Build();

    public int WindowCount => Windows.Count;

    public int ConvertedWindows => Windows.Count(window => window.Converted);

    public int WindowsWithPoison => Windows.Count(window => window.PoisonEffectId is not null);

    public int BroodingShadowsCasts { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.BroodingShadows))]
    private void OnBroodingShadowsCast(CastEvent castEvent)
    {
        if (castEvent.Fake) return;

        BroodingShadowsCasts++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.BroodingShadowsBuff))]
    private void OnStealthApplied(ApplyBuffEvent buffEvent) =>
        _openSpan ??= Open(buffEvent.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.BroodingShadowsBuff))]
    private void OnStealthRefreshed(RefreshBuffEvent buffEvent) =>
        _openSpan ??= Open(buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.BroodingShadowsBuff))]
    private void OnStealthRemoved(RemoveBuffEvent buffEvent)
    {
        if (_openSpan is null) return;

        _openSpan.ClosedAt = Math.Max(_openSpan.OpenedAt, buffEvent.Timestamp);
        _openSpan = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.Backstab), nameof(Spells.WidowBite), nameof(Spells.SkitteringBlades)])]
    private void OnBuilderCast(CastEvent castEvent)
    {
        if (castEvent.Fake) return;
        if (_openSpan is not { ConvertingAbilityId: null } span) return;

        span.ConvertingAbilityId = castEvent.Ability.Id;
        span.ConvertingCastAt = castEvent.Timestamp;
        span.ExpectedPoisonEffectId = PoisonOf(castEvent.Ability.Id);
        _lastConverted = span;
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.SkitteringBladesPoison)])]
    private void OnPoisonApplied(ApplyDebuffEvent debuffEvent) => LinkPoison(debuffEvent);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.SkitteringBladesPoison)])]
    private void OnPoisonRefreshed(RefreshDebuffEvent debuffEvent) => LinkPoison(debuffEvent);

    private void LinkPoison(BuffEvent debuffEvent)
    {
        if (_lastConverted is not { PoisonEffectId: null, ConvertingCastAt: { } castAt, ExpectedPoisonEffectId: { } expected } span)
            return;

        if (debuffEvent.Ability.Id != expected) return;

        var elapsed = debuffEvent.Timestamp - castAt;
        if (elapsed < 0 || elapsed > PoisonLinkWindowMs) return;

        span.PoisonEffectId = expected;
    }

    private static int? PoisonOf(int builderAbilityId)
    {
        if (builderAbilityId == Spells.WidowBite.Id) return Spells.WidowBitePoison.FSLID;
        if (builderAbilityId == Spells.SkitteringBlades.Id) return Spells.SkitteringBladesPoison.FSLID;
        return null;
    }

    private StealthSpan Open(int timestamp)
    {
        var span = new StealthSpan(timestamp);
        _spans.Add(span);
        return span;
    }

    private List<StealthWindow> Build()
    {
        var windows = new List<StealthWindow>(_spans.Count);
        foreach (var span in _spans)
        {
            windows.Add(new StealthWindow
            {
                OpenedAt = span.OpenedAt,
                ClosedAt = span.ClosedAt ?? Math.Max(span.OpenedAt, Pull.EndTime),
                ConvertingAbilityId = span.ConvertingAbilityId,
                ConvertingCastAt = span.ConvertingCastAt,
                PoisonEffectId = span.PoisonEffectId,
            });
        }
        return windows;
    }

    private sealed class StealthSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int? ConvertingAbilityId { get; set; }
        public int? ConvertingCastAt { get; set; }
        public int? ExpectedPoisonEffectId { get; set; }
        public int? PoisonEffectId { get; set; }
    }
}

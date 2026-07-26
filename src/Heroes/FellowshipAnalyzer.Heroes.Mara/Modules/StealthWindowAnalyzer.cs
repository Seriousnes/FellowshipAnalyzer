using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// One Brooding Shadows stealth window: the stretch the stealth buff stood, the builder that was
/// opened out of it, and the poison that builder laid.
/// </summary>
public sealed record StealthWindow
{
    public required int OpenedAt { get; init; }
    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    /// <summary>
    /// The first Backstab, Widow's Bite or Skittering Blades cast inside the window, or <c>null</c>
    /// when the window went by without one.
    /// </summary>
    public int? ConvertingAbilityId { get; init; }

    /// <summary>The converting cast's timestamp, or <c>null</c> when the window was never converted.</summary>
    public int? ConvertingCastAt { get; init; }

    /// <summary>
    /// The poison the converting builder laid, as an effect FSLID, or <c>null</c> when that builder's
    /// own poison never landed in the linking horizon after it. A stealth Backstab lays Caustic Poison,
    /// which is a damage effect rather than a tracked aura, so a converted Backstab window always
    /// reports no poison.
    /// </summary>
    public int? PoisonEffectId { get; init; }

    /// <summary>Whether a builder was opened out of this stealth window.</summary>
    public bool Converted => ConvertingAbilityId is not null;
}

/// <summary>
/// Measures how Mara's Brooding Shadows stealth windows were spent. A window opens when the stealth
/// buff lands on the player and closes on its removal, capped at <see cref="Analyzer.Pull"/>'s end
/// time when the removal is never logged. The window is converted by the first Backstab, Widow's Bite
/// or Skittering Blades cast inside it, which is what turns the stealth into an empowered builder; a
/// window that closes without one is the mistake this reports.
/// <para>
/// A converted window is then matched to the poison its own builder lays - Seething Poison for a
/// Widow's Bite, Volatile Poison for a Skittering Blades - applied or refreshed by the player within
/// <see cref="PoisonLinkWindowMs"/> of the converting cast. The link is anchored on the cast rather
/// than the window because the poison can land after the short stealth buff has already fallen off,
/// and it is keyed to the builder so the next global's poison cannot be credited here.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class StealthWindowAnalyzer : Analyzer
{
    /// <summary>
    /// How long after the converting cast a poison application still counts as that cast's poison.
    /// Wide enough to absorb travel and server ordering, narrow enough that the next builder's poison
    /// cannot be credited to this window.
    /// </summary>
    public const int PoisonLinkWindowMs = 1000;

    private readonly List<StealthSpan> _spans = [];

    private StealthSpan? _openSpan;
    private StealthSpan? _lastConverted;

    private IReadOnlyList<StealthWindow>? _windows;

    /// <summary>Every stealth window on the pull, in encounter order.</summary>
    public IReadOnlyList<StealthWindow> Windows => _windows ??= Build();

    public int WindowCount => Windows.Count;

    /// <summary>Windows that produced an empowered builder.</summary>
    public int ConvertedWindows => Windows.Count(window => window.Converted);

    /// <summary>Converted windows whose builder laid a tracked poison.</summary>
    public int WindowsWithPoison => Windows.Count(window => window.PoisonEffectId is not null);

    /// <summary>
    /// Brooding Shadows casts on the pull, whether or not a stealth buff was observed for them.
    /// A cast with no window behind it is a charge spent with nothing logged coming out of it.
    /// </summary>
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

    /// <summary>
    /// The poison a stealth-opened builder lays, or <c>null</c> for Backstab, whose Caustic Poison is
    /// dealt as damage rather than as a debuff aura there is any way to observe.
    /// </summary>
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

    /// <summary>A stealth window while it is still being accumulated from the event stream.</summary>
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

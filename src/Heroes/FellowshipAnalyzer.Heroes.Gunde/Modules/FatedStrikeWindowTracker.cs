using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Gunde.Statistics;

using Items = FellowshipAnalyzer.Core.Common.Spells.Items;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Tracks the window granted by the Fated Strike weapon, which most Gunde parses run. Its active,
/// also called Fated Strike, applies the Glorious Purpose self-buff for a few seconds, and abilities
/// cast while that buff is up have their cooldowns accelerated. The window is therefore worth
/// spending on the abilities whose cooldowns are worth pulling forward rather than on filler.
/// </summary>
/// <remarks>
/// Only the five rotational abilities the acceleration matters for are classified: Grim Carve,
/// Blood Arc and Heart Splitter as priority, Double Strike and Reaver's Edge as filler. Movement,
/// defensives and the long cooldowns are neither, so casting them inside a window is not a choice
/// this measures and they are ignored rather than counted against the share. Fated Strike casts are
/// counted separately from the buff so a log that carries the cast but not the buff still evidences
/// the weapon.
/// <para>
/// A window opens on every buff application, because the weapon is off cooldown far longer than the
/// buff lasts and a second application is therefore a second activation even when the first window's
/// removal never reached the log. A refresh only opens a window when none is live, so a refresh
/// landing inside a live window resets its clock instead of counting again.
/// </para>
/// <para>
/// A window is bounded by <see cref="WindowDurationMs"/> plus <see cref="SlackMs"/> rather than held
/// open until a removal arrives, so a log that drops the removal stops classifying casts at the
/// point the buff would have expired instead of classifying the rest of the pull.
/// </para>
/// <para>
/// <c>BurstWindowAnalyzer</c> takes the opposite policy on a re-apply, swallowing it into the live
/// window. Reign in Blood is a long cooldown whose buff cannot physically be re-applied while it is
/// still running, so a second application there is a logging artefact; Glorious Purpose is a weapon
/// proc that genuinely can fire again, so a second application here is a second activation.
/// </para>
/// </remarks>
public sealed partial class FatedStrikeWindowTracker : EventSubscriber
{
    /// <summary>Duration of the Glorious Purpose buff; live windows measure 6.01 seconds.</summary>
    public const int WindowDurationMs = 6_000;

    /// <summary>
    /// Tolerance added to <see cref="WindowDurationMs"/> when deciding whether a cast is still
    /// inside the window, covering the drift between the buff's nominal and observed durations.
    /// </summary>
    public const int SlackMs = 500;

    private int? _windowOpenedAt;

    /// <summary>Glorious Purpose windows opened, counting one still open when the log ended.</summary>
    public int Windows { get; private set; }

    /// <summary>Fated Strike activations, independent of whether the buff was logged.</summary>
    public int FatedStrikeCasts { get; private set; }

    /// <summary>Grim Carve, Blood Arc and Heart Splitter casts made inside a window.</summary>
    public int PriorityCasts { get; private set; }

    /// <summary>Double Strike and Reaver's Edge casts made inside a window.</summary>
    public int FillerCasts { get; private set; }

    /// <summary>Grim Carve, Blood Arc, Heart Splitter, Double Strike and Reaver's Edge casts made inside a window.</summary>
    public int ClassifiedCasts => PriorityCasts + FillerCasts;

    /// <summary>Share (0-1) of classified in-window casts that were priority abilities.</summary>
    public double PriorityShare => ClassifiedCasts > 0 ? (double)PriorityCasts / ClassifiedCasts : 0;

    public override Type? StatisticsComponentType =>
        Windows > 0 || FatedStrikeCasts > 0 ? typeof(FatedStrikeStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.Items;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Items.FatedStrike))]
    private void OnFatedStrikeCast(CastEvent castEvent) => FatedStrikeCasts++;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Items.GloriousPurpose))]
    private void OnWindowApplied(ApplyBuffEvent buffEvent)
    {
        _windowOpenedAt = buffEvent.Timestamp;
        Windows++;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Items.GloriousPurpose))]
    private void OnWindowRefreshed(RefreshBuffEvent buffEvent)
    {
        if (!IsWindowLive(buffEvent.Timestamp))
            Windows++;

        _windowOpenedAt = buffEvent.Timestamp;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Items.GloriousPurpose))]
    private void OnWindowRemoved(RemoveBuffEvent buffEvent) => _windowOpenedAt = null;

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.GrimCarve),
        nameof(Spells.BloodArc),
        nameof(Spells.HeartSplitter)])]
    private void OnPriorityCast(CastEvent castEvent)
    {
        if (IsWindowLive(castEvent.Timestamp))
            PriorityCasts++;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.DoubleStrike),
        nameof(Spells.ReaverEdge)])]
    private void OnFillerCast(CastEvent castEvent)
    {
        if (IsWindowLive(castEvent.Timestamp))
            FillerCasts++;
    }

    private bool IsWindowLive(int timestamp) =>
        _windowOpenedAt is { } opened && timestamp - opened is >= 0 and <= WindowDurationMs + SlackMs;
}

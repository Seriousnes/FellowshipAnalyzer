using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Keyed service interface for hero-specific analysis.
/// Implementations are registered per hero ID and orchestrate
/// the full analysis pipeline for that hero's mechanics.
/// </summary>
public interface IHeroAnalyzer
{
    /// <summary>
    /// Report-level actor master data: every player, NPC, and pet the report names.
    /// Must be populated before calling <see cref="Analyze"/>.
    /// </summary>
    IReadOnlyList<ReportActor> Actors { get; set; }

    /// <summary>Actor display names keyed by actor id, derived from <see cref="Actors"/>.</summary>
    IReadOnlyDictionary<int, string> ActorNames { get; }

    /// <summary>The FellowshipLogs actor id of the player this analysis is being run for.</summary>
    int PlayerId { get; set; }

    /// <summary>The combatant record for <see cref="PlayerId"/>, resolved from the report's combatant list.</summary>
    Combatant SelectedCombatant { get; }

    /// <summary>
    /// The Razor component type to render for the Guide tab.
    /// <c>null</c> indicates this hero has no implemented analysis yet (WIP).
    /// Hosts can read this without invoking <see cref="Analyze"/> to short-circuit
    /// the analysis pipeline (e.g. skip fetching events).
    /// </summary>
    Type? GuideComponent { get; }

    /// <summary>
    /// Every ended pull in encounter order. Enumerated by the report header's pull-filter select.
    /// </summary>
    IReadOnlyList<Pull> Pulls { get; }

    /// <summary>
    /// When set, generated analyzer surface streams are clamped to this pull so a report view can
    /// crop to a single encounter. <c>null</c> leaves the whole fight in view.
    /// </summary>
    Pull? SelectedPull { get; set; }

    /// <summary>Runs the full normalize-dispatch-accumulate pipeline over <paramref name="events"/> and produces the hero's analysis result.</summary>
    /// <param name="events">The player's combat log event stream for the report.</param>
    /// <param name="playerId">The FellowshipLogs actor id of the player being analyzed.</param>
    /// <param name="fight">The report fight the events belong to.</param>
    /// <returns>The completed hero analysis, ready for the guide and statistics views.</returns>
    Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, ReportFight fight);
}

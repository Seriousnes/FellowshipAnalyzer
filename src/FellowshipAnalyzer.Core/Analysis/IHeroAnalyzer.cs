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
    /// Report-level actor name lookup, keyed by actor ID.
    /// Must be populated before calling <see cref="Analyze"/>.
    /// </summary>
    Dictionary<int, string> ActorNames { get; set; }
    int PlayerId { get; set; }
    Combatant? SelectedCombatant { get; }

    /// <summary>
    /// The Razor component type to render for the Guide tab.
    /// <c>null</c> indicates this hero has no implemented analysis yet (WIP).
    /// Hosts can read this without invoking <see cref="Analyze"/> to short-circuit
    /// the analysis pipeline (e.g. skip fetching events).
    /// </summary>
    Type? GuideComponent { get; }

    Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, ReportFight fight);
}

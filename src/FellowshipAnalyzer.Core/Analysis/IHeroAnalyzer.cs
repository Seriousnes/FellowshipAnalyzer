using FellowshipAnalyzer.Core.Events;

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

    Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, int fightStartTime);
}

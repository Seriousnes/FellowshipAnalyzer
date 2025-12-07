using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Keyed service interface for hero-specific analysis.
/// Implementations are registered per hero ID and orchestrate
/// the full analysis pipeline for that hero's mechanics.
/// </summary>
public interface IHeroAnalyzer
{
    string HeroId { get; }
    Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, int fightStartTime);
}

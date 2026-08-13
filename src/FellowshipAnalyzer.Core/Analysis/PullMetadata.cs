using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// What the parse produced about one <see cref="PullStartEvent"/>: the <see cref="Analyzer"/> instances
/// retained on it, keyed by surface type (the topmost ancestor deriving directly from
/// <see cref="Analyzer"/>). Populated by the parser as the pull ends.
/// </summary>
public sealed class PullMetadata
{
    private readonly Dictionary<Type, Analyzer> _analyzers = [];

    /// <summary>The analyzer retained for <paramref name="surfaceType"/>, or <c>null</c> when no analyzer of that surface ran on the pull.</summary>
    public Analyzer? GetAnalyzer(Type surfaceType) =>
        _analyzers.TryGetValue(surfaceType, out var analyzer) ? analyzer : null;

    internal void SetAnalyzer(Type surfaceType, Analyzer analyzer) => _analyzers[surfaceType] = analyzer;
}

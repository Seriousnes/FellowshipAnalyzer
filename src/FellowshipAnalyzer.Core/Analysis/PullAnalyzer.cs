namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A retained <see cref="Analyzer"/> instance paired with the <see cref="Pull"/> it ran on.
/// </summary>
public readonly record struct PullAnalyzer<T>(Pull Pull, T Analyzer) where T : Analyzer;

/// <summary>
/// The cross-pull stream of one analyzer surface, in pull order. The parser builds it
/// incrementally as each pull ends; multiple shape-specialized Analyzers sharing the same
/// surface type on disjoint pulls contribute to one list.
/// </summary>
public sealed class PullAnalyzerList<T> : List<PullAnalyzer<T>>
    where T : Analyzer;

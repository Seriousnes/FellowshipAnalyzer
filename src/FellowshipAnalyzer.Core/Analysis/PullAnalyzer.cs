using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A retained analyzer instance paired with the <see cref="PullStartEvent"/> it ran on.
/// <typeparamref name="T"/> is the analyzer's surface type: a concrete <see cref="Analyzer"/> subclass,
/// or an <see cref="IAnalyzerSurface"/> marker interface shared by several shape-specialized analyzers.
/// </summary>
public readonly record struct PullAnalyzer<T>(PullStartEvent Pull, T Analyzer) where T : IAnalyzerSurface;

/// <summary>
/// The cross-pull stream of one analyzer surface, in pull order. The parser builds it
/// incrementally as each pull ends; multiple shape-specialized Analyzers sharing the same
/// surface (a base class or an <see cref="IAnalyzerSurface"/> marker) on disjoint pulls contribute
/// to one list.
/// </summary>
public sealed class PullAnalyzerList<T> : List<PullAnalyzer<T>>
    where T : IAnalyzerSurface;

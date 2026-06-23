namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A single <see cref="Analyzer{TResult}"/> result paired with the <see cref="Pull"/> it was
/// produced on.
/// </summary>
public readonly record struct PullResult<T>(Pull Pull, T Result) where T : IResult;

/// <summary>
/// The cross-pull stream of one result type, in pull order. The parser builds it incrementally as
/// each pull ends; multiple shape-specialized Analyzers producing the same <typeparamref name="TResult"/>
/// on disjoint pulls contribute to one list.
/// </summary>
public sealed class PullResultList<TResult> : List<PullResult<TResult>>
    where TResult : IResult;

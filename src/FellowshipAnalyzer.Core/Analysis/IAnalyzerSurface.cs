namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The constraint for a pull-analyzer surface: the type a <see cref="PullAnalyzer{T}"/> stream and
/// per-pull accessor are keyed on. <see cref="Analyzer"/> implements it, so every analyzer class is
/// itself a valid surface. A dedicated interface that extends this marker becomes a shared surface:
/// independent <see cref="Analyzer"/> subclasses implementing one such interface, on disjoint
/// <c>[ForPull]</c> filters, feed a single cross-pull stream (<c>parser.{Surface}s</c>) and per-pull
/// accessor (<c>pull.{Surface}</c>) without a common base class. An analyzer must implement at most
/// one such surface interface.
/// </summary>
public interface IAnalyzerSurface;

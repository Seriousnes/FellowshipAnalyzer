using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Shared surface for Rend analysis. <see cref="RendUptimeAnalyzer"/> (boss pulls) and
/// <see cref="RendSpreadAnalyzer"/> (multi-target pulls) each implement this marker so both are
/// exposed under one <c>Parser.RendAnalyzers</c> stream and <c>pull.RendAnalyzer</c> accessor,
/// without sharing a base class. The two analyses share no members; consumers switch on the
/// concrete type.
/// </summary>
public interface IRendAnalyzer : IAnalyzerSurface;

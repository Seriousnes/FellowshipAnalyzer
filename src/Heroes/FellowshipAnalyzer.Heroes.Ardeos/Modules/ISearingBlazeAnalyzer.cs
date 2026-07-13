using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Shared surface for Searing Blaze analysis. <see cref="SearingBlazeUptimeAnalyzer"/> (boss pulls)
/// and <see cref="SearingBlazeSpreadAnalyzer"/> (multi-target pulls) each implement this marker so
/// both are exposed under one <c>Parser.SearingBlazeAnalyzers</c> stream and <c>pull.SearingBlazeAnalyzer</c>
/// accessor, without sharing a base class. The two analyses share no members; consumers switch on the
/// concrete type.
/// </summary>
public interface ISearingBlazeAnalyzer : IAnalyzerSurface;

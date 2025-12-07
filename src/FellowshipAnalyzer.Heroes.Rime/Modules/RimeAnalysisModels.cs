namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed record RimeAnalyzerFinding(
    string Severity,
    string Message,
    int? Timestamp = null);

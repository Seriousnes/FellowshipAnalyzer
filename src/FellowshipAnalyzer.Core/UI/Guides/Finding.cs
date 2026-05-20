namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// An analyzer finding displayed in a <see cref="FindingsList"/>.
/// </summary>
public sealed record Finding(
    string Severity,
    string Message,
    string? Timestamp = null);

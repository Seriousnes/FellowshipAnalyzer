namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>A single choice in a <see cref="ButtonGroup{T}"/>.</summary>
public sealed record ButtonGroupOption<T>(T Value, string Label);

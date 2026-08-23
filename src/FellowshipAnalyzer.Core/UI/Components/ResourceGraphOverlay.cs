namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// A per-second rate series drawn over a <see cref="ResourceGraph"/>'s resource line as a smooth
/// curve against its own opposite-side axis, e.g. damage taken per second over Toughness.
/// </summary>
/// <param name="Name">The series name shown in the legend and tooltip.</param>
/// <param name="Points">The rate samples, in encounter order.</param>
public sealed record ResourceGraphOverlay(string Name, List<ResourceGraphOverlayPoint> Points);

/// <summary>One sample of a <see cref="ResourceGraphOverlay"/> series.</summary>
/// <param name="Timestamp">The sample's absolute report timestamp in milliseconds.</param>
/// <param name="Value">The per-second rate at that instant.</param>
public sealed record ResourceGraphOverlayPoint(int Timestamp, double Value);

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Represents the deserialized events from a combat log fight, with progress status.
/// </summary>
public sealed record EventsResult(List<Event> Events, bool InProgress);

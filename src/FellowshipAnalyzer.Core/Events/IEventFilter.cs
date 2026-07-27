namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marker interface implemented by <see cref="Event"/>, letting event-filtering code target it without depending on the concrete <see cref="Event"/> base type.</summary>
public interface IEventFilter { }

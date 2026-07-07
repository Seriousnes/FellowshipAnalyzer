using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Collects debug annotations attached to specific events by any analyzer module.
/// Consumed by the Debug tab in the UI to render colored indicator dots and event details.
/// </summary>
public sealed class DebugAnnotations : Module
{
    private readonly Dictionary<Module, Dictionary<Event, List<DebugAnnotation>>> _annotations = [];

    /// <summary>
    /// Attaches a debug annotation to an event, attributed to a specific module.
    /// </summary>
    public void AddAnnotation(Module module, Event @event, DebugAnnotation annotation)
    {
        if (!_annotations.TryGetValue(module, out var eventMap))
        {
            eventMap = [];
            _annotations[module] = eventMap;
        }

        if (!eventMap.TryGetValue(@event, out var list))
        {
            list = [];
            eventMap[@event] = list;
        }

        list.Add(annotation);
    }

    /// <summary>
    /// Returns all annotated events for a specific module.
    /// </summary>
    public ModuleAnnotations GetModuleAnnotations(Module module)
    {
        var annotatedEvents = _annotations.TryGetValue(module, out var eventMap)
            ? [.. eventMap.Select(kvp => new AnnotatedEvent(kvp.Key, kvp.Value))]
            : (IReadOnlyList<AnnotatedEvent>)[];

        return new ModuleAnnotations(module, annotatedEvents);
    }

    /// <summary>
    /// Returns all annotated events grouped by module.
    /// </summary>
    public IReadOnlyList<ModuleAnnotations> GetAll() =>
        [.. _annotations.Keys.Select(GetModuleAnnotations)];
}

/// <summary>
/// A debug annotation that can be attached to a specific event.
/// </summary>
/// <param name="Color">CSS color string for the indicator dot (e.g. "#e74c3c", "orange").</param>
/// <param name="Summary">Short text description shown in the tooltip.</param>
/// <param name="Details">Optional longer description with additional context.</param>
/// <param name="Priority">
/// When multiple annotations exist for the same event in the same module,
/// the one with the highest priority determines the dot color and summary.
/// </param>
public sealed record DebugAnnotation(
    string Color,
    string Summary,
    string? Details = null,
    int Priority = 0);

/// <summary>A single event together with all debug annotations attached to it.</summary>
public sealed record AnnotatedEvent(Event Event, IReadOnlyList<DebugAnnotation> Annotations);

/// <summary>All annotated events attributed to a single analyzer module.</summary>
public sealed record ModuleAnnotations(Module Module, IReadOnlyList<AnnotatedEvent> AnnotatedEvents);

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Result of a hero analysis run. Contains the guide component type, auto-collected
/// statistics components, and the full list of active modules.
/// </summary>
public sealed class HeroAnalysisResult
{
    /// <summary>
    /// The Razor component type to render for the Guide tab.
    /// Rendered via DynamicComponent on Report.razor.
    /// <c>null</c> when the hero has no guide implemented yet — Report.razor
    /// renders a WIP fallback in that case.
    /// </summary>
    public required Type? GuideComponentType { get; init; }

    /// <summary>
    /// Active modules that have a statistics component, paired with the metadata needed
    /// to render them on the Statistics tab: which Razor component, which section to group
    /// under, and the ordinal position within that section.
    /// </summary>
    public required IReadOnlyList<StatisticEntry> Statistics { get; init; }

    public required IReadOnlyList<Module> Modules { get; init; }

    /// <summary>
    /// All events processed during analysis, including fabricated events (e.g. GlobalCooldownEvent,
    /// UpdateSpellUsableEvent). Ordered by timestamp. Exposed for Timeline rendering.
    /// </summary>
    public required IReadOnlyList<Event> Events { get; init; }

    /// <summary>
    /// Debug annotations collected during analysis. Null if the DebugAnnotations module was not active.
    /// Exposed for the Debug tab in the UI.
    /// </summary>
    public DebugAnnotations? DebugAnnotations { get; init; }
}

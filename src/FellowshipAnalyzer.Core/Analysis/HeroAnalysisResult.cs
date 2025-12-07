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
    /// </summary>
    public required Type GuideComponentType { get; init; }

    /// <summary>
    /// Active modules that have a statistics component, paired with their component type.
    /// Rendered on the Statistics tab via CascadingValue + DynamicComponent.
    /// </summary>
    public required IReadOnlyList<(Module Module, Type ComponentType)> Statistics { get; init; }

    public required IReadOnlyList<Module> Modules { get; init; }
}

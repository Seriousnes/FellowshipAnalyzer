using FellowshipAnalyzer.Core.Analysis;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// Base class for auto-collected statistics components rendered on the Statistics tab.
/// The module instance is provided via CascadingValue from the Statistics rendering loop.
/// </summary>
/// <typeparam name="T">The analyzer/module type this statistics component visualises.</typeparam>
public abstract class AnalyzerStatistic<T> : ComponentBase where T : Module
{
    /// <summary>The untyped module instance cascaded in from the Statistics rendering loop.</summary>
    [CascadingParameter]
    public Module Module { get; set; } = null!;

    /// <summary>The cascaded <see cref="Module"/> cast to the concrete <typeparamref name="T"/> analyzer type.</summary>
    protected T Analyzer => (T)Module;
}

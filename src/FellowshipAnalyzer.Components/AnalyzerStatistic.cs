using FellowshipAnalyzer.Core.Analysis;
using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Components;

/// <summary>
/// Base class for auto-collected statistics components rendered on the Statistics tab.
/// The module instance is provided via CascadingValue from the Statistics rendering loop.
/// </summary>
/// <typeparam name="T">The analyzer/module type this statistics component visualises.</typeparam>
public abstract class AnalyzerStatistic<T> : ComponentBase where T : Module
{
    [CascadingParameter]
    public Module Module { get; set; } = null!;

    protected T Analyzer => (T)Module;
}

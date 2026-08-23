namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Registers a dungeon-lifetime <see cref="Module"/> on a parser; the generator constructs and wires it
/// once for the whole report. A type that subscribes to events derives from <see cref="Analyzer"/> and
/// is registered with <see cref="AddAnalyzerAttribute{T}"/> instead (diagnostic FA0019).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddModuleAttribute<T> : Attribute where T : Module
{
    /// <summary>The module type registered by this attribute.</summary>
    public Type ModuleType { get; } = typeof(T);
}
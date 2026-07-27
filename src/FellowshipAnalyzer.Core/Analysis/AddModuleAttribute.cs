using FellowshipAnalyzer.Core.Analysis;

/// <summary>Registers a parse-lifetime <see cref="Module"/> on a hero parser; the generator constructs and wires it once for the whole report.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddModuleAttribute<T> : Attribute where T : Module
{
    /// <summary>The module type registered by this attribute.</summary>
    public Type ModuleType { get; } = typeof(T);
}
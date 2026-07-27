using FellowshipAnalyzer.Core.Analysis;

/// <summary>Registers a pull-lifetime <see cref="Analyzer"/> on a hero parser; the generator constructs a fresh instance for each pull.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddAnalyzerAttribute<T> : Attribute where T : Analyzer
{
    /// <summary>The analyzer type registered by this attribute.</summary>
    public Type AnalyzerType { get; } = typeof(T);
}

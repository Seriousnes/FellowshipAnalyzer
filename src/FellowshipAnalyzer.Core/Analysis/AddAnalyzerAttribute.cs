namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Registers an <see cref="Analyzer"/> on a parser, which is every type that subscribes to events.
/// <see cref="ForPullAttribute"/> on the registered type decides the lifetime: with it the generator
/// constructs a fresh instance for each matching pull, without it one instance for the whole report.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddAnalyzerAttribute<T> : Attribute where T : Analyzer
{
    /// <summary>The analyzer type registered by this attribute.</summary>
    public Type AnalyzerType { get; } = typeof(T);
}

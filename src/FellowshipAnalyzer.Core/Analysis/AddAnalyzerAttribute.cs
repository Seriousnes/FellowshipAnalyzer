using FellowshipAnalyzer.Core.Analysis;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddAnalyzerAttribute<T> : Attribute where T : Analyzer
{
    public Type AnalyzerType { get; } = typeof(T);
}

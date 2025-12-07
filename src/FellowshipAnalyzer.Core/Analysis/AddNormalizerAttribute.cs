namespace FellowshipAnalyzer.Core.Analysis;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddNormalizerAttribute<T> : Attribute where T : IEventNormalizer
{
    public Type NormalizerType { get; } = typeof(T);
}

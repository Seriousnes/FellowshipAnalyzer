namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Registers an <see cref="IEventNormalizer"/> on a hero parser to preprocess the event stream before dispatch.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddNormalizerAttribute<T> : Attribute where T : IEventNormalizer
{
    /// <summary>The normalizer type registered by this attribute.</summary>
    public Type NormalizerType { get; } = typeof(T);
}

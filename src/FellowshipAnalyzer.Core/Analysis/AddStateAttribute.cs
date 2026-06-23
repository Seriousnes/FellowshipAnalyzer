using FellowshipAnalyzer.Core.Analysis;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddStateAttribute<T> : Attribute where T : Module
{
    public Type StateType { get; } = typeof(T);
}

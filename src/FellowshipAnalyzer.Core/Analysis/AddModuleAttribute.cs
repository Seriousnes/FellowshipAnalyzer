using FellowshipAnalyzer.Core.Analysis;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddModuleAttribute<T> : Attribute where T : Module
{
    public Type ModuleType { get; } = typeof(T);
}
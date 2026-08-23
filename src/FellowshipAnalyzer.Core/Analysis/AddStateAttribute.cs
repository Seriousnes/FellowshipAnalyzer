namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Registers a dungeon-lifetime <see cref="Module"/> that holds shared state on a parser, an exact synonym of <see cref="AddModuleAttribute{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AddStateAttribute<T> : Attribute where T : Module
{
    /// <summary>The module type registered by this attribute.</summary>
    public Type StateType { get; } = typeof(T);
}

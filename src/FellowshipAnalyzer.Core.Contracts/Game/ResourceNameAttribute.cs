namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// Declares the upstream game-data resource name(s) that map onto a
/// <see cref="ResourceTypes"/> slot. Used by the offline spell-data tooling to resolve a
/// resource flavor name (e.g. "Winter Orbs") to its abstract slot; never read at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ResourceNameAttribute(params string[] names) : Attribute
{
    /// <summary>The upstream resource flavor names represented by the decorated slot.</summary>
    public string[] Names { get; } = names;
}

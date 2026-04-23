namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Marks a <see langword="static partial"/> class as the target for the merged spell registry
/// source generator. The generator will emit forwarding properties and an <c>All</c> dictionary
/// covering every <see cref="ISpellRegistry"/> implementor visible in the compilation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSpellRegistryAttribute : Attribute { }

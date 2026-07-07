namespace FellowshipAnalyzer.Core.Common;

/// <summary>
/// Marks a <see langword="static partial"/> class as the merge target for a source-generated
/// registry. The generator scans all implementations of <typeparamref name="T"/> visible in the
/// compilation and emits forwarding properties and an <c>All</c> dictionary on the decorated class.
/// </summary>
/// <typeparam name="T">
/// The registry marker interface whose implementors should be consolidated,
/// e.g. <see cref="Spells.ISpellRegistry"/> or <see cref="Items.IItemRegistry"/>.
/// </typeparam>
/// <remarks>
/// Entry types must expose an <c>FSLID</c> property — this is used as the key for the
/// generated <c>All</c> dictionary. The value type of <c>All</c> is inferred as the lowest common
/// ancestor of all entry property types found across all implementors.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateRegistryAttribute<T> : Attribute where T : class { }

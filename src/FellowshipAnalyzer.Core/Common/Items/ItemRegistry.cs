using System.Collections.Frozen;
using System.Reflection;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Common.Items;

/// <summary>
/// Central merged item lookup table. Discovers all <see cref="IItemRegistry"/> implementors in
/// loaded assemblies and indexes their static <see cref="Item"/> properties by ID.
/// </summary>
/// <remarks>
/// To add items, create a class that implements <see cref="IItemRegistry"/> and declare static
/// <see cref="Item"/> properties on it. The registry auto-discovers them via reflection at startup.
/// <para>
/// If hero-specific item classes live in assemblies not yet loaded at first access, call
/// <see cref="EnsureAssembly"/> before the first registry lookup.
/// </para>
/// </remarks>
public static class ItemRegistry
{
    private static FrozenDictionary<int, Item> _table;

    private static FrozenDictionary<(int Id, HeroName Hero), Item> _heroTable;

    static ItemRegistry() => (_table, _heroTable) = BuildTables();

    /// <summary>The full set of registered items, keyed by item ID.</summary>
    public static FrozenDictionary<int, Item> All => _table;

    /// <summary>
    /// Gets the item with the given ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no item with the given ID is registered.</exception>
    public static Item Get(int id)
    {
        if (_table.TryGetValue(id, out var item))
            return item;

        throw new KeyNotFoundException(
            $"Item with ID {id} is not registered. " +
            "Ensure the IItemRegistry class declaring this item is in a loaded assembly before the first registry access. " +
            "Use ItemRegistry.MaybeGet(id) if the item may not always be registered.");
    }

    /// <summary>
    /// Gets the item with the given ID, or <see langword="null"/> if it is not registered.
    /// </summary>
    public static Item? MaybeGet(int id) =>
        _table.TryGetValue(id, out var item) ? item : null;

    /// <summary>Gets <paramref name="hero"/>'s own item with the given ID, or <see langword="null"/> if the ID names no item for that hero.</summary>
    public static Item? MaybeGet(int id, HeroName? hero) =>
        hero is { } name && _heroTable.TryGetValue((id, name), out var own) ? own
        : _table.TryGetValue(id, out var item) && (hero is null || item.Hero is null) ? item
        : null;

    /// <summary>
    /// Gets the item with the given ID.
    /// </summary>
    public static bool TryGet(int id, out Item? item)
    {
        var found = _table.TryGetValue(id, out var result);
        item = result;
        return found;
    }

    /// <summary>
    /// Ensures the given assembly is scanned for <see cref="IItemRegistry"/> implementors and
    /// rebuilds the registry. Call this before the first registry access when hero-specific item
    /// classes live in assemblies that are not yet loaded.
    /// </summary>
    public static void EnsureAssembly(Assembly assembly)
    {
        (_table, _heroTable) = BuildTables(extraAssembly: assembly);
    }

    private static (FrozenDictionary<int, Item> ById, FrozenDictionary<(int Id, HeroName Hero), Item> ByHero) BuildTables(
        Assembly? extraAssembly = null)
    {
        var registryType = typeof(IItemRegistry);
        var itemType = typeof(Item);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies().AsEnumerable();
        if (extraAssembly is not null)
            assemblies = assemblies.Append(extraAssembly);

        var entries = new Dictionary<int, Item>();
        var heroEntries = new Dictionary<(int Id, HeroName Hero), Item>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!registryType.IsAssignableFrom(type) || type.IsInterface)
                    continue;

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (prop.PropertyType != itemType)
                        continue;

                    if (prop.GetValue(null) is Item item)
                    {
                        entries[item.Id] = item;

                        if (item.Hero is { } hero)
                            heroEntries[(item.Id, hero)] = item;
                    }
                }
            }
        }

        return (entries.ToFrozenDictionary(), heroEntries.ToFrozenDictionary());
    }
}

/// <summary>
/// Marker interface for item registry classes. Implement this on any class that declares static
/// <see cref="Item"/> properties to have them auto-discovered by <see cref="ItemRegistry"/>.
/// </summary>
public interface IItemRegistry
{
}

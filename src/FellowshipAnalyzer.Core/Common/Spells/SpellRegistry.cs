using System.Collections.Frozen;
using System.Reflection;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Central merged spell lookup table. Discovers all <see cref="ISpellRegistry"/> implementors in
/// loaded assemblies and indexes their static <see cref="Spell"/> properties by ID.
/// </summary>
/// <remarks>
/// To add spells, create a class that implements <see cref="ISpellRegistry"/> and declare static
/// <see cref="Spell"/> properties on it. The registry auto-discovers them via reflection at startup.
/// <para>
/// If hero-specific spell classes live in assemblies not yet loaded at first access, call
/// <see cref="EnsureAssembly"/> before the first registry lookup.
/// </para>
/// </remarks>
public static class SpellRegistry
{
    private static FrozenDictionary<int, Spell> _table = BuildTable();

    /// <summary>The full set of registered spells, keyed by spell ID.</summary>
    public static IReadOnlyDictionary<int, Spell> All => _table;

    /// <summary>
    /// Gets the spell with the given ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no spell with the given ID is registered.</exception>
    public static Spell Get(int id)
    {
        if (_table.TryGetValue(id, out var spell))
            return spell;

        throw new KeyNotFoundException(
            $"Spell with ID {id} is not registered. " +
            "Ensure the ISpellRegistry class declaring this spell is in a loaded assembly before the first registry access. " +
            "Use SpellRegistry.MaybeGet(id) if the spell may not always be registered.");
    }

    /// <summary>
    /// Gets the spell with the given ID, or <see langword="null"/> if it is not registered.
    /// </summary>
    public static Spell? MaybeGet(int id) =>
        _table.TryGetValue(id, out var spell) ? spell : null;

    /// <summary>
    /// Gets the spell with the given ID.
    /// </summary>
    public static bool TryGet(int id, out Spell? spell)
    {
        var found = _table.TryGetValue(id, out var result);
        spell = result;
        return found;
    }

    /// <summary>
    /// Ensures the given assembly is scanned for <see cref="ISpellRegistry"/> implementors and
    /// rebuilds the registry. Call this before the first registry access when hero-specific spell
    /// classes live in assemblies that are not yet loaded.
    /// </summary>
    public static void EnsureAssembly(Assembly assembly)
    {
        _table = BuildTable(extraAssembly: assembly);
    }

    private static FrozenDictionary<int, Spell> BuildTable(Assembly? extraAssembly = null)
    {
        var registryType = typeof(ISpellRegistry);
        var spellType = typeof(Spell);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies().AsEnumerable();
        if (extraAssembly is not null)
            assemblies = assemblies.Append(extraAssembly);

        var entries = new Dictionary<int, Spell>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!registryType.IsAssignableFrom(type) || type.IsInterface)
                    continue;

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!prop.PropertyType.IsAssignableTo(spellType))
                        continue;

                    if (prop.GetValue(null) is Spell spell)
                    {
                        var key = spell is Effect effect ? effect.SpellId : spell.Id;
                        entries[key] = spell;
                    }
                }
            }
        }

        return entries.ToFrozenDictionary();
    }
}

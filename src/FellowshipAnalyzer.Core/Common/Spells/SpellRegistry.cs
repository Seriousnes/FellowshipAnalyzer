namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Central spell lookup table. Delegates to the source-generated <see cref="Spells.All"/>
/// dictionary, which is built at compile time from every <see cref="ISpellRegistry"/>
/// implementor in the assembly — no runtime reflection required.
/// </summary>
public static class SpellRegistry
{
    /// <summary>The full set of registered spells, keyed by <see cref="Spell.FSLID"/>.</summary>
    public static IReadOnlyDictionary<int, Spell> All => Spells.All;

    /// <summary>
    /// Gets the spell with the given FSLID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no spell with the given FSLID is registered.</exception>
    public static Spell Get(int id)
    {
        if (Spells.All.TryGetValue(id, out var spell))
            return spell;

        throw new KeyNotFoundException(
            $"Spell with FSLID {id} is not registered. " +
            "Ensure the ISpellRegistry class declaring this spell is in FellowshipAnalyzer.Core. " +
            "Use SpellRegistry.MaybeGet(id) if the spell may not always be registered.");
    }

    /// <summary>
    /// Gets the spell with the given FSLID, or <see langword="null"/> if it is not registered.
    /// </summary>
    public static Spell? MaybeGet(int id) =>
        Spells.All.GetValueOrDefault(id);

    /// <summary>
    /// Gets the spell with the given FSLID.
    /// </summary>
    public static bool TryGet(int id, out Spell? spell)
    {
        var found = Spells.All.TryGetValue(id, out var result);
        spell = result;
        return found;
    }
}

public interface ISpellRegistry
{
}

using System.Reflection;
using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.SpellData.Tests;

/// <summary>
/// A single reflected entry from a hand-written <c>Spells</c> registry class.
/// </summary>
public record SpellSnapshot(
    string Member,
    int FSLID,
    string Name,
    string Icon,
    int? SpiritCost,
    int? WinterOrbCost,
    int? AnimaCost,
    int? FocusCost);

/// <summary>
/// Reflects over the generated <c>FellowshipAnalyzer.Core.Common.Spells.{Hero}.Spells</c>
/// registries and captures static <see cref="Spell"/>-typed properties as snapshots for
/// migration-diff cross-validation. The original hand-authored values were validated during override curation;
/// <c>ReproducibilityTests</c> pins <c>spelldb.json</c>.
/// </summary>
public static class HandWrittenRegistrySnapshot
{
    private static readonly Assembly CoreAssembly = typeof(Spell).Assembly;

    /// <summary>
    /// Returns all static <see cref="Spell"/>-typed properties declared on
    /// <c>FellowshipAnalyzer.Core.Common.Spells.{hero}.Spells</c>.
    /// </summary>
    public static IReadOnlyList<SpellSnapshot> For(string hero)
    {
        var typeName = $"FellowshipAnalyzer.Core.Common.Spells.{hero}.Spells";
        var type = CoreAssembly.GetType(typeName, throwOnError: false);
        if (type is null)
        {
            if (!string.Equals(hero, "Ardeos", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Registry type '{typeName}' was not found in Core; every hero except Ardeos must have a generated Spells class.");
            return [];
        }
        return Enumerate(type);
    }

    private static IReadOnlyList<SpellSnapshot> Enumerate(Type type)
    {
        var spellType = typeof(Spell);
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => spellType.IsAssignableFrom(p.PropertyType))
            .Select(p =>
            {
                var spell = (Spell)p.GetValue(null)!;
                return new SpellSnapshot(
                    p.Name,
                    spell.FSLID.Value,
                    spell.Name,
                    spell.Icon,
                    spell.SpiritCost,
                    spell.WinterOrbCost,
                    spell.AnimaCost,
                    spell.FocusCost);
            })
            .ToList();
    }
}

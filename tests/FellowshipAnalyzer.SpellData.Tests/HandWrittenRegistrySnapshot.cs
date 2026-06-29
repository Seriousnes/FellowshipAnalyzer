using System.Reflection;
using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.SpellData.Tests;

/// <summary>
/// A single reflected entry from a hand-written <c>Spells</c> registry class.
/// </summary>
public record SpellSnapshot(
    string Member,
    int Guid,
    string Name,
    string Icon,
    int? SpiritCost,
    int? WinterOrbCost,
    int? AnimaCost,
    int? FocusCost);

/// <summary>
/// Reflects over the hand-written <c>FellowshipAnalyzer.Core.Common.Spells.{Hero}.Spells</c>
/// registries and captures static <see cref="Spell"/>-typed properties as snapshots for
/// migration-diff comparison.
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
        return type is null ? [] : Enumerate(type);
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
                    spell.Guid,
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

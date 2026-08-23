namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>A hero's known abilities, keyed by spell id, used to resolve ability names and timings when the combat log itself is incomplete.</summary>
/// <param name="Hero">The hero this ability set belongs to.</param>
/// <param name="Abilities">The hero's abilities, keyed by spell id.</param>
public sealed record HeroAnalysisDefinition(
    HeroName Hero,
    Dictionary<int, AbilityDefinition> Abilities)
{
    /// <summary>Looks up the ability with the given spell id, or <c>null</c> if this hero has none defined for it.</summary>
    public AbilityDefinition? FindAbility(int abilityId)
    {
        return Abilities.TryGetValue(abilityId, out var ability) ? ability : null;
    }

    /// <summary>Resolves the display name for <paramref name="abilityId"/> from <see cref="Abilities"/>, falling back to <paramref name="fallbackName"/> or a generic <c>"Spell {id}"</c> label when unknown.</summary>
    public string ResolveAbilityName(int abilityId, string? fallbackName = null)
    {
        if (Abilities.TryGetValue(abilityId, out var ability))
        {
            return ability.Name;
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? $"Spell {abilityId}" : fallbackName;
    }
}

/// <summary>Known gameplay metadata for a single ability, used to resolve names and timings the combat log does not carry directly.</summary>
/// <param name="SpellId">The ability's spell id.</param>
/// <param name="Name">The ability's display name.</param>
/// <param name="CooldownSeconds">The ability's cooldown in seconds, or <c>null</c> if unknown.</param>
/// <param name="Charges">The number of charges the ability has, or <c>null</c> if unknown.</param>
/// <param name="CastTimeMs">The ability's cast time in milliseconds, or <c>null</c> if unknown.</param>
public sealed record AbilityDefinition(
    int SpellId,
    string Name,
    double? CooldownSeconds = null,
    int? Charges = null,
    int? CastTimeMs = null);
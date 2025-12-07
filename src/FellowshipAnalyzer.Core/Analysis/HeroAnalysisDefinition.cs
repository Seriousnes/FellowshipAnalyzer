namespace FellowshipAnalyzer.Core.Analysis;

public sealed record HeroAnalysisDefinition(
    string HeroId,
    IReadOnlyDictionary<int, AbilityDefinition> Abilities)
{
    public AbilityDefinition? FindAbility(int abilityId)
    {
        return Abilities.TryGetValue(abilityId, out var ability) ? ability : null;
    }

    public string ResolveAbilityName(int abilityId, string? fallbackName = null)
    {
        if (Abilities.TryGetValue(abilityId, out var ability))
        {
            return ability.Name;
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? $"Spell {abilityId}" : fallbackName;
    }
}

public sealed record AbilityDefinition(
    int SpellId,
    string Name,
    double? CooldownSeconds = null,
    int? Charges = null,
    int? CastTimeMs = null);
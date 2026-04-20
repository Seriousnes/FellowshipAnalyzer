namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base module that defines a hero's spellbook — the set of abilities available
/// to the player along with their gameplay metadata (cooldowns, GCD, categories, etc.).
/// Hero analyzers override <see cref="Spellbook"/> to provide spec-specific entries.
/// </summary>
public class Abilities : Module
{
    private Dictionary<int, SpellbookAbility> _abilities = [];
    public static GcdInfo StandardGcd => new() { Base = 1500.0 };

    public override void Initialize()
    {
        _abilities = [];

        foreach (var entry in Spellbook())
        {
            if (!entry.Enabled)
            {
                continue;
            }

            _abilities[entry.PrimarySpell.Guid] = entry;

            if (entry.AdditionalSpells is not null)
            {
                foreach (var extra in entry.AdditionalSpells)
                {
                    _abilities[extra.Guid] = entry;
                }
            }
        }
    }

    /// <summary>
    /// Override this in hero-specific subclasses to define the spellbook.
    /// </summary>
    public virtual IEnumerable<SpellbookAbility> Spellbook() => [];

    /// <summary>
    /// Looks up an ability by spell ID. Returns null if the spell is not in the spellbook.
    /// </summary>
    public SpellbookAbility? GetAbility(int? spellId)
    {
        return spellId.HasValue ? _abilities.GetValueOrDefault(spellId.Value) : null;
    }

    /// <summary>
    /// Returns all registered abilities (enabled entries only, deduplicated by primary spell ID).
    /// </summary>
    public IEnumerable<SpellbookAbility> GetAbilities()
    {
        return _abilities.Values.Distinct();
    }

    /// <summary>
    /// Gets the expected cooldown for a spell at the given haste level (in seconds).
    /// Returns 0 if the spell has no cooldown or is not in the spellbook.
    /// </summary>
    public double GetExpectedCooldown(int spellId, double haste = 1.0)
    {
        var ability = GetAbility(spellId);
        return ability?.GetCooldown(haste) ?? 0;
    }

    /// <summary>
    /// Gets the max charges for a spell. Returns 1 if the spell is not in the spellbook.
    /// </summary>
    public int GetMaxCharges(int spellId)
    {
        var ability = GetAbility(spellId);
        return ability?.GetCharges(Owner.SelectedCombatant) ?? 1;
    }
}

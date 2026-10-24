using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base module that defines a hero's spellbook - the set of abilities available
/// to the player along with their gameplay metadata (cooldowns, GCD, categories, etc.).
/// Hero analyzers override <see cref="Spellbook"/> to provide spec-specific entries.
/// </summary>
public class Abilities : Module
{
    private Dictionary<int, SpellbookAbility>? _abilities;
    /// <summary>The default global cooldown, 1500ms, shared by most abilities that don't define their own.</summary>
    public static GcdInfo StandardGcd => new() { Base = 1500.0 };

    /// <summary>
    /// Override this in hero-specific subclasses to define the spellbook.
    /// </summary>
    public virtual IEnumerable<SpellbookAbility> Spellbook() => [];

    private Dictionary<int, SpellbookAbility> AbilitiesByGuid
    {
        get
        {
            if (_abilities is not null) return _abilities;
            var abilities = new Dictionary<int, SpellbookAbility>();
            foreach (var spellbookEntry in Spellbook())
            {
                if (!spellbookEntry.Enabled) continue;
                var entry = Granted(spellbookEntry);
                abilities[entry.PrimarySpell.FSLID] = entry;
                if (entry.AdditionalSpells is not null)
                {
                    foreach (var extra in entry.AdditionalSpells)
                        abilities[extra.FSLID] = entry;
                }
            }
            if (Owner?.SelectedCombatantIfKnown is not null) _abilities = abilities;
            return abilities;
        }
    }

    /// <summary>
    /// <paramref name="entry"/> with its primary spell's <see cref="Spell.ChargeGrant"/> applied when the
    /// selected player holds the grant: the granted charges, unless the spellbook states more, and the
    /// grant's Haste placement. A <see cref="SpellbookAbility.CooldownReducedByHaste"/> the spellbook sets
    /// still overrides the placement.
    /// </summary>
    private SpellbookAbility Granted(SpellbookAbility entry)
    {
        if (entry.PrimarySpell.ChargeGrant is not { } grant || Owner?.SelectedCombatantIfKnown is not { } player)
            return entry;

        var held = grant.Source switch
        {
            GrantSource.Talent => player.HasTalent(grant.Id),
            GrantSource.LegendaryItem => player.HasLegendary(grant.Id),
            _ => false,
        };

        return held
            ? entry with
            {
                PrimarySpell = entry.PrimarySpell with
                {
                    Charges = Math.Max(entry.PrimarySpell.Charges, grant.Charges),
                    CooldownHaste = grant.CooldownHaste,
                },
            }
            : entry;
    }

    /// <summary>
    /// Looks up an ability by spell ID. Returns null if the spell is not in the spellbook.
    /// </summary>
    public SpellbookAbility? GetAbility(int? spellId)
    {
        return spellId.HasValue ? AbilitiesByGuid.GetValueOrDefault(spellId.Value) : null;
    }

    /// <summary>
    /// Returns all registered abilities (enabled entries only, deduplicated by primary spell ID).
    /// </summary>
    public IEnumerable<SpellbookAbility> GetAbilities()
    {
        return AbilitiesByGuid.Values.Distinct();
    }

    /// <summary>
    /// Gets the expected cooldown for a spell at the given haste level (in seconds).
    /// Returns 0 if the spell has no cooldown or is not in the spellbook.
    /// </summary>
    public double GetExpectedCooldown(int spellId, double haste = 0.0)
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
        return ability?.Charges ?? 1;
    }
}

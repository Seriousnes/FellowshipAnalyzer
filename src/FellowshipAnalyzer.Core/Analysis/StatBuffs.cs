using System.Collections.Frozen;

using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Every stat effect the Season 3 game data describes that leaves a trace in the combat log, keyed by the
/// FSLID the log records for it. <see cref="StatTracker"/> registers all four tables at construction, so a
/// stat effect is tracked without a hero having to opt in, and a hero adds only what the shared data does
/// not already cover.
/// </summary>
/// <remarks>
/// Magnitudes come from the Season 3 <c>gear_data.json</c> and <c>hero_data.json</c> constant blocks and
/// from the Fellowship codex effect records; ids come from the generated spell and item registries rather
/// than literals, so a rename in <c>data/spelldb.json</c> breaks the build instead of silently missing a
/// buff.
/// <para>
/// Effects with no log trace are out of scope here, for two separate reasons. A gear effect that adds a
/// stat <em>rating</em> is already inside the combatantinfo totals <see cref="StatTracker"/> seeds each
/// pull from, so registering it would count it twice: across the report corpus a player's reported Crit,
/// Haste, Expertise, and Spirit exceed the sum of their gear attributes by exactly their Amethyst, Topaz,
/// Emerald, and Sapphire gem tier. A gear effect that adds a flat <em>percentage</em> is not in
/// those totals, but it also never fires an apply or remove event, so an event-driven table has
/// nothing to key on; <see cref="PassiveBonusTracker"/> resolves those from gem power, set pieces, and the
/// dungeon's modifiers, and <see cref="TheMonarchAnalyzer"/> from the blessing level.
/// Effects scoped to a subset of a hero's abilities are out as well, because a global pool would apply them
/// to every cast. Damage reduction and movement speed are out because the game expresses them as
/// multipliers on damage taken and movement rate, which <c>CombatMath</c> owns and this additive channel
/// cannot express faithfully.
/// </para>
/// </remarks>
public static class StatBuffs
{
    /// <summary>Effects that add a stat rating.</summary>
    public static FrozenDictionary<int, StatBuff> Ratings { get; } = new Dictionary<int, StatBuff>
    {
        [Items.SeizedOpportunityBuff.FSLID] = new()
        {
            Crit = ByTraitRank(Items.SeizedOpportunityTrait, 21, 39, 57, 75),
        },
        [Items.InspiredAllegianceBuff.FSLID] = new()
        {
            Haste = HasteRatingOf(Items.InspiredAllegianceBuff, Items.InspiredAllegianceTrait),
        },
        [Items.FocusedHasteBuff.FSLID] = new()
        {
            Haste = HasteRatingOf(Items.FocusedHasteBuff, Items.HuntersFocusTrait),
            PerStack = true,
        },
        [Items.NavigatorsIntuitionCrit.FSLID] = new()
        {
            Crit = ByTraitRank(Items.NavigatorsIntuitionTrait, 28, 66, 104, 142),
        },
        [Items.NavigatorsIntuitionHaste.FSLID] = new()
        {
            Haste = HasteRatingOf(Items.NavigatorsIntuitionHaste, Items.NavigatorsIntuitionTrait),
        },
        [Items.NavigatorsIntuitionExpertise.FSLID] = new()
        {
            Expertise = ByTraitRank(Items.NavigatorsIntuitionTrait, 28, 66, 104, 142),
        },
        [Items.NavigatorsIntuitionSpirit.FSLID] = new()
        {
            Spirit = ByTraitRank(Items.NavigatorsIntuitionTrait, 28, 66, 104, 142),
        },
    }.ToFrozenDictionary();

    /// <summary>Effects that scale a stat rating by a multiplier.</summary>
    public static FrozenDictionary<int, StatMultiplierBuff> Multipliers { get; } = new Dictionary<int, StatMultiplierBuff>
    {
        [Items.MightOfTheMinotaur.FSLID] = new() { MainStat = 1.02 },
        [Items.MightOfTheMinotaurII.FSLID] = new() { MainStat = 1.06 },
        [Items.AncestralSurge.FSLID] = new() { MainStat = 1.08 },
        [Items.AncestralSurgeII.FSLID] = new() { MainStat = 1.24 },
    }.ToFrozenDictionary();

    /// <summary>Effects that add a flat percentage on top of the rating-derived percentage.</summary>
    public static FrozenDictionary<int, StatPercentageBuff> Percentages { get; } = new Dictionary<int, StatPercentageBuff>
    {
        [Spells.SpiritOfHeroism.FSLID] = HasteOf(Spells.SpiritOfHeroism),
        [Spells.SpiritOfHeroismNotTank.FSLID] = HasteOf(Spells.SpiritOfHeroismNotTank),
        [Spells.SpiritOfHeroismHealer.FSLID] = HasteOf(Spells.SpiritOfHeroismHealer),
        [Spells.SpiritOfHeroismXavian.FSLID] = HasteOf(Spells.SpiritOfHeroismXavian),

        [Spells.RuneRush.FSLID] = HasteOf(Spells.RuneRush),
        [Spells.ShadowsDefeat.FSLID] = HasteOf(Spells.ShadowsDefeat),
        [Spells.EmpoweredMinionVictoryRush.FSLID] = HasteOf(Spells.EmpoweredMinionVictoryRush),
        [Spells.StormsFury.FSLID] = new() { Crit = 0.20 },

        [Items.AdrenalineRush.FSLID] = HasteOf(Items.AdrenalineRush),
        [Items.AdrenalineRushII.FSLID] = HasteOf(Items.AdrenalineRushII),
        [Items.EssenceOfTheVirtuoso.FSLID] = HasteOf(Items.EssenceOfTheVirtuoso),
        [Items.EssenceOfTheVirtuosoII.FSLID] = HasteOf(Items.EssenceOfTheVirtuosoII),
        [Items.FirstStrike.FSLID] = new() { Expertise = 0.05, PerStack = true },
        [Items.FirstStrikeII.FSLID] = new() { Expertise = 0.15 },
        [Items.HarmoniousSoul.FSLID] = HarmoniousSoul(Items.HarmoniousSoul),
        [Items.HarmoniousSoulII.FSLID] = HarmoniousSoul(Items.HarmoniousSoulII),

        [Items.DarkProphecy.FSLID] = HasteOf(Items.DarkProphecy),

        [Items.TheWayfarer.FSLID] = HasteOf(Items.TheWayfarer, blessing: "The Wayfarer"),
        [Items.TheTrickster.FSLID] = new()
        {
            Crit = ByBlessingLevel("The Trickster", 0.025, 0.04, 0.064, 0.10),
        },
        [Items.ThePhilosopher.FSLID] = new()
        {
            Crit = ThePhilosopher,
            Haste = ThePhilosopher,
            Expertise = ThePhilosopher,
        },
    }.ToFrozenDictionary();

    /// <summary>Effects that contribute to the Ability Cooldown Reduction or Cooldown Acceleration pools.</summary>
    public static FrozenDictionary<int, CooldownBuff> Cooldowns { get; } =
        new Dictionary<int, CooldownBuff>().ToFrozenDictionary();

    /// <summary>
    /// The flat percentage buff of <paramref name="effect"/>, taking each Haste modifier of the effect in the
    /// export under its operation: <see cref="ModifierOp.Additive"/> sets <see cref="StatPercentageBuff.Haste"/>,
    /// <see cref="ModifierOp.Multiplicitive"/> sets <see cref="StatPercentageBuff.HasteMultiplier"/>,
    /// <see cref="ModifierOp.MultiplyCompound"/> sets <see cref="StatPercentageBuff.HasteCompound"/> and
    /// <see cref="ModifierOp.AddFinal"/> sets <see cref="StatPercentageBuff.HasteAddFinal"/>. A modifier with
    /// ranks reads the equipped tier of <paramref name="blessing"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export gives <paramref name="effect"/> no Haste modifier.</exception>
    private static StatPercentageBuff HasteOf(Spell effect, bool perStack = false, string? blessing = null)
    {
        Func<StatBuffContext, int>? rank = blessing is null ? null : context => context.Combatant.BlessingLevel(blessing);
        BuffVal? haste = null;
        BuffVal? multiplier = null;
        BuffVal? compound = null;
        BuffVal? addFinal = null;

        foreach (var modifier in EffectData.ModifiersOf(effect.Id))
        {
            if (modifier.Attribute != StatAttribute.Haste) continue;

            var value = ValueOf(modifier, rank, effect);
            switch (modifier.Op)
            {
                case ModifierOp.Additive: haste = value; break;
                case ModifierOp.Multiplicitive: multiplier = value; break;
                case ModifierOp.MultiplyCompound: compound = value; break;
                case ModifierOp.AddFinal: addFinal = value; break;
            }
        }

        if (haste is null && multiplier is null && compound is null && addFinal is null)
            throw new InvalidOperationException($"The export gives effect {effect.Id} no Haste modifier.");

        return new()
        {
            Haste = haste,
            HasteMultiplier = multiplier,
            HasteCompound = compound,
            HasteAddFinal = addFinal,
            PerStack = perStack,
        };
    }

    /// <summary>
    /// The Haste rating of <paramref name="effect"/> in the export, by the rank of <paramref name="trait"/>
    /// on the player's gear.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export gives <paramref name="effect"/> no Haste rating modifier.</exception>
    private static BuffVal HasteRatingOf(Spell effect, Spell trait)
    {
        foreach (var modifier in EffectData.ModifiersOf(effect.Id))
        {
            if (modifier.Attribute == StatAttribute.HasteRating)
                return ValueOf(modifier, context => context.Combatant.TraitRank(trait.FSLID), effect);
        }

        throw new InvalidOperationException($"The export gives effect {effect.Id} no Haste rating modifier.");
    }

    private static BuffVal ValueOf(AttributeModifier modifier, Func<StatBuffContext, int>? rank, Spell effect)
    {
        if (modifier.Ranks is not { Length: > 0 } ranks) return modifier.Value ?? 0.0;

        if (rank is null)
            throw new InvalidOperationException($"Effect {effect.Id} has a modifier with ranks and no rank source.");

        return new((Func<StatBuffContext, double>)(context => rank(context) is var r and > 0
            ? ranks[Math.Min(r, ranks.Length) - 1]
            : 0.0));
    }

    /// <summary>
    /// The Sapphire trait that grants Critical Strike, Haste, Expertise, and Spirit for five seconds each
    /// time an enemy the player is in combat with dies. Both its ranks grant the same 0.6% per stack and
    /// differ only in how many stacks they allow, which the log reports directly.
    /// </summary>
    private static StatPercentageBuff HarmoniousSoul(Spell effect) => new()
    {
        Crit = 0.006,
        Haste = HasteOf(effect, perStack: true).Haste,
        Expertise = 0.006,
        Spirit = 0.006,
        PerStack = true,
    };

    /// <summary>
    /// The Philosopher grants the same value of Critical Strike, Haste, and Expertise, sized by the player's
    /// Spirit stat when a MAJOR ability applied it: 0.2% for each step of Spirit, where the step narrows with
    /// the blessing's tier, and Spirit above half counts as half. It reads the Spirit stat rather than Spirit
    /// Points, since the haste it grants holds steady however full the Spirit bar is.
    /// </summary>
    private static BuffVal ThePhilosopher => new((Func<StatBuffContext, double>)(context =>
    {
        var perStep = context.Combatant.BlessingLevel("The Philosopher") switch
        {
            1 => 0.04,
            2 => 0.025,
            3 => 0.016,
            >= 4 => 0.01,
            _ => 0.0,
        };

        if (perStep <= 0) return 0.0;
        return Math.Min(context.Spirit, 0.5) / perStep * 0.002;
    }));

    /// <summary>
    /// Picks a blessing's magnitude from the tier the player's gear adds up to. The blessing is matched on its
    /// name because a report's blessing id is a per-hero loadout node id, not an id for the blessing itself:
    /// The Trickster is 4000033 on Gunde and 4000063 on Aeona.
    /// </summary>
    private static BuffVal ByBlessingLevel(string blessing, double one, double two, double three, double four) =>
        new((Func<StatBuffContext, double>)(context => context.Combatant.BlessingLevel(blessing) switch
        {
            1 => one,
            2 => two,
            3 => three,
            >= 4 => four,
            _ => 0.0,
        }));

    private static BuffVal ByTraitRank(Spell trait, double one, double two, double three, double four) =>
        new((Func<StatBuffContext, double>)(context => context.Combatant.TraitRank(trait.FSLID) switch
        {
            1 => one,
            2 => two,
            3 => three,
            >= 4 => four,
            _ => 0.0,
        }));
}

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
/// Emerald, and Sapphire gem tier. A gear effect that adds a flat <em>percentage</em> is not in that
/// snapshot, but it also never fires an apply or remove event, so an event-driven tracker has nothing to
/// key on; resolving those from gem power, set piece counts, and blessing levels is separate work.
/// Effects scoped to a subset of a hero's abilities are out as well, because a global pool would apply them
/// to every cast. Damage reduction and movement speed are out because the game expresses them as
/// multipliers on damage taken and movement rate, which <c>CombatMath</c> owns and this additive channel
/// cannot carry faithfully.
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
            Haste = ByTraitRank(Items.InspiredAllegianceTrait, 6, 10, 13, 17),
        },
        [Items.FocusedHasteBuff.FSLID] = new()
        {
            Haste = ByTraitRank(Items.HuntersFocusTrait, 4, 8, 12, 16),
            PerStack = true,
        },
        [Items.NavigatorsIntuitionCrit.FSLID] = new()
        {
            Crit = ByTraitRank(Items.NavigatorsIntuitionTrait, 28, 66, 104, 142),
        },
        [Items.NavigatorsIntuitionHaste.FSLID] = new()
        {
            Haste = ByTraitRank(Items.NavigatorsIntuitionTrait, 28, 66, 104, 142),
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
        [Spells.SpiritOfHeroism.FSLID] = SpiritOfHeroism,
        [Spells.SpiritOfHeroismNotTank.FSLID] = SpiritOfHeroism,
        [Spells.SpiritOfHeroismHealer.FSLID] = SpiritOfHeroism,
        [Spells.SpiritOfHeroismXavian.FSLID] = SpiritOfHeroism,

        [Items.AdrenalineRush.FSLID] = new() { Haste = 0.03 },
        [Items.AdrenalineRushII.FSLID] = new() { Haste = 0.09 },
        [Items.FirstStrike.FSLID] = new() { Expertise = 0.05, PerStack = true },
        [Items.FirstStrikeII.FSLID] = new() { Expertise = 0.15 },
        [Items.HarmoniousSoul.FSLID] = HarmoniousSoul,
        [Items.HarmoniousSoulII.FSLID] = HarmoniousSoul,

        [Items.DarkProphecy.FSLID] = new() { Haste = 0.25 },

        [Items.TheWayfarer.FSLID] = new()
        {
            Haste = ByBlessingLevel("The Wayfarer", 0.06, 0.10, 0.16, 0.25),
        },
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
    /// The 30% haste every hero gains for 20 seconds on activating their Spirit ability. The shared Spirit
    /// passive applies it under one of four ids depending on the hero, and the haste grant is identical
    /// across all four; only the riders differ.
    /// </summary>
    private static StatPercentageBuff SpiritOfHeroism => new() { Haste = 0.30 };

    /// <summary>
    /// The Sapphire trait that grants Critical Strike, Haste, Expertise, and Spirit for five seconds each
    /// time an enemy the player is in combat with dies. Both its ranks grant the same 0.6% per stack and
    /// differ only in how many stacks they allow, which the log reports directly.
    /// </summary>
    private static StatPercentageBuff HarmoniousSoul => new()
    {
        Crit = 0.006,
        Haste = 0.006,
        Expertise = 0.006,
        Spirit = 0.006,
        PerStack = true,
    };

    /// <summary>
    /// The Philosopher grants the same value of Critical Strike, Haste, and Expertise, sized by the Spirit
    /// the player held when a MAJOR ability applied it: 0.2% per whole step of Spirit, where the step
    /// narrows with the blessing's level, and Spirit above half counts as half.
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
        return Math.Min(context.ResourceFraction(ResourceTypes.Spirit), 0.5) / perStep * 0.002;
    }));

    /// <summary>
    /// Picks a blessing's magnitude from the level the player's gear reports. The blessing is matched on its
    /// name because a report's blessing id is a per-hero loadout node id, not an id for the blessing itself:
    /// The Trickster is 4000033 on Gunde and 4000063 on Aeona. Only levels 1 and 2 appear anywhere in the
    /// report corpus, so the level 3 and 4 values are from the game data alone.
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

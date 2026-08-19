using System.Collections.Frozen;

using FellowshipAnalyzer.Core.Common.Spells;

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
/// to every cast.
/// </para>
/// </remarks>
public static class StatBuffs
{
    /// <summary>Effects that add a stat rating.</summary>
    public static FrozenDictionary<int, StatBuff> Ratings { get; } =
        new Dictionary<int, StatBuff>().ToFrozenDictionary();

    /// <summary>Effects that scale a stat rating by a multiplier.</summary>
    public static FrozenDictionary<int, StatMultiplierBuff> Multipliers { get; } =
        new Dictionary<int, StatMultiplierBuff>().ToFrozenDictionary();

    /// <summary>Effects that add a flat percentage on top of the rating-derived percentage.</summary>
    public static FrozenDictionary<int, StatPercentageBuff> Percentages { get; } = new Dictionary<int, StatPercentageBuff>
    {
        [Spells.SpiritOfHeroism.FSLID] = SpiritOfHeroism,
        [Spells.SpiritOfHeroismNotTank.FSLID] = SpiritOfHeroism,
        [Spells.SpiritOfHeroismHealer.FSLID] = SpiritOfHeroism,
        [Spells.SpiritOfHeroismXavian.FSLID] = SpiritOfHeroism,
    }.ToFrozenDictionary();

    /// <summary>Effects that contribute to the Ability Cooldown Reduction or Cooldown Acceleration pools.</summary>
    public static FrozenDictionary<int, CooldownBuff> Cooldowns { get; } =
        new Dictionary<int, CooldownBuff>().ToFrozenDictionary();

    /// <summary>
    /// The 30% haste every hero gains for 20 seconds on activating their Spirit ability. The shared Spirit
    /// passive applies it under one of four ids depending on the hero's role, and the haste grant is
    /// identical across all four; only the riders differ.
    /// </summary>
    private static StatPercentageBuff SpiritOfHeroism => new() { Haste = 0.30 };
}

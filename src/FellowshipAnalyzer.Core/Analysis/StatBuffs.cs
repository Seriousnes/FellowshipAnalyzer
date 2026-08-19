using System.Collections.Frozen;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Every stat effect the Season 3 game data describes, keyed by the FSLID the combat log records for it.
/// <see cref="StatTracker"/> registers all four tables at construction, so a stat effect is tracked without
/// a hero having to opt in, and a hero adds only what the shared data does not already cover.
/// </summary>
/// <remarks>
/// Magnitudes come from the s3 <c>gear_data.json</c> and <c>hero_data.json</c> constant blocks and from the
/// Fellowship codex effect records; ids come from the generated spell and item registries rather than
/// literals, so a rename in <c>data/spelldb.json</c> breaks the build instead of silently missing a buff.
/// </remarks>
public static class StatBuffs
{
    /// <summary>Effects that add a stat rating.</summary>
    public static FrozenDictionary<int, StatBuff> Ratings { get; } = new Dictionary<int, StatBuff>().ToFrozenDictionary();

    /// <summary>Effects that scale a stat rating by a multiplier.</summary>
    public static FrozenDictionary<int, StatMultiplierBuff> Multipliers { get; } = new Dictionary<int, StatMultiplierBuff>().ToFrozenDictionary();

    /// <summary>Effects that add a flat percentage on top of the rating-derived percentage.</summary>
    public static FrozenDictionary<int, StatPercentageBuff> Percentages { get; } = new Dictionary<int, StatPercentageBuff>().ToFrozenDictionary();

    /// <summary>Effects that contribute to the Ability Cooldown Reduction or Cooldown Acceleration pools.</summary>
    public static FrozenDictionary<int, CooldownBuff> Cooldowns { get; } = new Dictionary<int, CooldownBuff>().ToFrozenDictionary();
}

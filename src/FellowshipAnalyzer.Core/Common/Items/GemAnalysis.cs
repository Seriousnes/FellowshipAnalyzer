using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>One of the six gem colours a player accumulates gem power in.</summary>
public enum GemType
{
    /// <summary>Ruby, whose ranks grant primary attribute, stamina, and health effects.</summary>
    Ruby,
    /// <summary>Amethyst, whose ranks grant critical strike effects.</summary>
    Amethyst,
    /// <summary>Topaz, whose ranks grant haste effects.</summary>
    Topaz,
    /// <summary>Emerald, whose ranks grant expertise, absorb, and cooldown reduction effects.</summary>
    Emerald,
    /// <summary>Sapphire, whose ranks grant spirit and Heroism effects.</summary>
    Sapphire,
    /// <summary>Diamond, whose ranks grant mixed secondary stat and relic effects.</summary>
    Diamond,
}

/// <summary>
/// Gem power thresholds for the ten ranks of every gem, from the S3 gear data
/// <c>Gems.&lt;Gem&gt;.Ranks</c>. The ladder is the same for all six colours: ranks 1-5 are the gem's five
/// traits, and ranks 6-10 are the same five again at their higher tier, each replacing its lower rank.
/// </summary>
public static class GemRankPower
{
    /// <summary>Gem power unlocking a gem's first rank.</summary>
    public const int Rank1 = 80;
    /// <summary>Gem power unlocking a gem's second rank.</summary>
    public const int Rank2 = 150;
    /// <summary>Gem power unlocking a gem's third rank.</summary>
    public const int Rank3 = 220;
    /// <summary>Gem power unlocking a gem's fourth rank.</summary>
    public const int Rank4 = 300;
    /// <summary>Gem power unlocking a gem's fifth rank.</summary>
    public const int Rank5 = 450;
    /// <summary>Gem power unlocking a gem's sixth rank.</summary>
    public const int Rank6 = 600;
    /// <summary>Gem power unlocking a gem's seventh rank.</summary>
    public const int Rank7 = 800;
    /// <summary>Gem power unlocking a gem's eighth rank.</summary>
    public const int Rank8 = 1000;
    /// <summary>Gem power unlocking a gem's ninth rank.</summary>
    public const int Rank9 = 1250;
    /// <summary>Gem power unlocking a gem's tenth rank, which is also the gem power cap.</summary>
    public const int Rank10 = 1500;
}

/// <summary>
/// One trait on a gem's rank ladder: the same bonus at a lower and a higher rank, the higher
/// <em>replacing</em> the lower rather than stacking with it. Which one the player has is decided by gem
/// power alone.
/// </summary>
/// <param name="BaseRank">The trait's lower rank.</param>
/// <param name="BaseRankPower">Gem power at which <paramref name="BaseRank"/> unlocks.</param>
/// <param name="UpgradedRank">The trait's higher rank, which replaces <paramref name="BaseRank"/>.</param>
/// <param name="UpgradedRankPower">Gem power at which <paramref name="UpgradedRank"/> replaces the lower rank.</param>
public sealed record GemTrait(Spell BaseRank, int BaseRankPower, Spell UpgradedRank, int UpgradedRankPower)
{
    /// <summary>True once <paramref name="gemPower"/> reaches this trait's lower rank.</summary>
    public bool IsUnlocked(int gemPower) => gemPower >= BaseRankPower;

    /// <summary>True once <paramref name="gemPower"/> reaches this trait's higher rank.</summary>
    public bool IsUpgraded(int gemPower) => gemPower >= UpgradedRankPower;

    /// <summary>The highest rank <paramref name="gemPower"/> has unlocked, or <c>null</c> while the trait is locked.</summary>
    public Spell? ActiveRank(int gemPower) =>
        IsUpgraded(gemPower) ? UpgradedRank
        : IsUnlocked(gemPower) ? BaseRank
        : null;

    /// <summary>
    /// Picks the value matching the unlocked rank: <paramref name="upgraded"/> at the higher rank,
    /// <paramref name="based"/> at the lower, and <paramref name="locked"/> while the trait is locked.
    /// </summary>
    public T ByRank<T>(int gemPower, T based, T upgraded, T locked) =>
        IsUpgraded(gemPower) ? upgraded
        : IsUnlocked(gemPower) ? based
        : locked;

    /// <summary>
    /// Every window of this trait's unlocked rank on <paramref name="unit"/>, clipped to
    /// <paramref name="from"/>..<paramref name="to"/>. Reads the windows <see cref="Combatants"/> tracks, so
    /// a rank the player entered the dungeon already carrying counts from the start and one still active at the
    /// end closes there. Empty while the trait is locked.
    /// </summary>
    public IEnumerable<AuraWindow> WindowsOn(Entity unit, int gemPower, int from, int to) =>
        ActiveRank(gemPower) is { } rank ? unit.GetAuraWindows(rank, from, to) : [];

    /// <summary>Total time this trait's unlocked rank was active on <paramref name="unit"/> over the dungeon, in milliseconds.</summary>
    public int UptimeOn(Entity unit, int gemPower, int from, int to) =>
        WindowsOn(unit, gemPower, from, to).Sum(window => window.Duration);
}

/// <summary>
/// Implemented by the one analyzer per gem colour. Each gem's analyzer measures every trait on its ladder
/// that the log can account for, and its statistics card reads this surface to head the card and to decide
/// which trait sections to render.
/// </summary>
public interface IGemAnalyzer
{
    /// <summary>The gem colour this analyzer covers.</summary>
    GemType Gem { get; }
    /// <summary>The player's accumulated power in this gem, from the combatantinfo snapshot.</summary>
    int GemPower { get; }
}

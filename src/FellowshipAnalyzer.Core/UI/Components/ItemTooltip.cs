using Fellowship.SDK;
using Fellowship.SDK.Client;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// States an equipped item to the codex, so its tooltip reads as the item the player wore rather
/// than as the item at its shipped defaults.
/// </summary>
public static class ItemTooltip
{
    /// <summary>
    /// The stat an item states as armour, which the tooltip takes as its own parameter rather than
    /// as one of the item's stat lines.
    /// </summary>
    private const string ArmorStat = "Armor";

    /// <summary>What the codex is to draw for <paramref name="item"/> as <paramref name="hero"/> wore it.</summary>
    /// <remarks>
    /// Each attribute is stated at the magnitude the log records, which fills the item's declared
    /// slots in order and draws the rest as rolled lines. A blessing is left off: the log names it by
    /// an ability rank id, which only the codex can read back to a blessing.
    /// </remarks>
    public static ItemTooltipRequest For(Item item, HeroName? hero)
    {
        var rolls = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
        decimal? armor = null;

        foreach (var attribute in item.Attributes)
        {
            if (attribute.Name.Equals(ArmorStat, StringComparison.OrdinalIgnoreCase))
            {
                armor = attribute.Value;
                continue;
            }

            if (!rolls.TryGetValue(attribute.Name, out var rated))
            {
                rolls[attribute.Name] = rated = [];
            }

            rated.Add(attribute.Value);
        }

        return new ItemTooltipRequest
        {
            Hero = hero?.ToString(),
            Rarity = item.Quality.ToString(),
            ItemLevel = item.ItemLevel,
            Upgrades = item.Upgrades,
            MaxUpgrades = item.MaxUpgrades,
            NoSocket = !item.HasGemSocket,
            Gem = item.Gem?.Id,
            Armor = armor,
            Stats = rolls.ToDictionary(
                stat => stat.Key,
                stat => new StatRolls { Rating = stat.Value },
                StringComparer.OrdinalIgnoreCase),
            Modifiers = item.Traits.Count > 0
                ? [.. item.Traits.Select(trait =>
                    new ItemModifier(ModifierKind.Trait, new FSLID(trait.Id).NativeId, trait.Rank))]
                : null,
        };
    }

    /// <summary>What the codex is to draw for an item the report states nothing more about than its id.</summary>
    public static ItemTooltipRequest For(HeroName? hero) => new() { Hero = hero?.ToString() };
}

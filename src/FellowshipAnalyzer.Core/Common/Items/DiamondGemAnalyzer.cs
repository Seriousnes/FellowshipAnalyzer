using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Diamond gem contributed. Harmonious Soul is the trait the log accounts for, a
/// secondary stat buff that stacks once per nearby ally; the log carries its windows and stack changes but
/// no unbuffed comparison, so the measure is how long it held and how high it stacked. Diamond's other
/// ranks raise primary attribute and armor, reduce incoming magic damage, and shorten relic cooldowns, which
/// are unmodelled while relic abilities are.
/// </summary>
public sealed partial class DiamondGemAnalyzer : Analyzer, IGemAnalyzer
{
    /// <summary>Grants critical strike, haste, expertise, and spirit, stacking once per nearby ally.</summary>
    public static readonly GemTrait HarmoniousSoul = new(
        Items.HarmoniousSoul, GemRankPower.Rank1,
        Items.HarmoniousSoulII, GemRankPower.Rank6);

    /// <inheritdoc/>
    public GemType Gem => GemType.Diamond;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Diamond;

    /// <summary>Total time Harmonious Soul was active over the fight, in milliseconds.</summary>
    public int HarmoniousSoulUptimeMs =>
        HarmoniousSoul.UptimeOn(Owner.SelectedCombatant, GemPower, Owner.FightStartTime, Owner.FightEndTime);

    /// <summary>Harmonious Soul's share of the fight, as a fraction.</summary>
    public double HarmoniousSoulUptime =>
        Owner.FightDurationMs > 0 ? (double)HarmoniousSoulUptimeMs / Owner.FightDurationMs : 0;

    /// <summary>Time-weighted mean stack count while Harmonious Soul was active.</summary>
    public double HarmoniousSoulAverageStacks =>
        HarmoniousSoulUptimeMs > 0 ? (double)StackMilliseconds / HarmoniousSoulUptimeMs : 0;

    private long StackMilliseconds
    {
        get
        {
            if (HarmoniousSoul.ActiveRank(GemPower) is not { } rank) return 0;

            var total = 0L;
            foreach (var buff in Owner.SelectedCombatant.GetBuffHistory(rank.FSLID))
            {
                var windowEnd = Math.Min(buff.End ?? Owner.FightEndTime, Owner.FightEndTime);
                var history = buff.StackHistory;

                for (var i = 0; i < history.Count; i++)
                {
                    var from = Math.Max(history[i].Timestamp, Owner.FightStartTime);
                    var until = Math.Min(i + 1 < history.Count ? history[i + 1].Timestamp : windowEnd, windowEnd);

                    if (until > from)
                        total += (long)history[i].Stacks * (until - from);
                }
            }

            return total;
        }
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}

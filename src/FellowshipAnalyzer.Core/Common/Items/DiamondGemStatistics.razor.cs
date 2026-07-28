using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Diamond gem contributed. Harmonious Soul is the trait the log accounts for, a
/// secondary stat buff that stacks once per nearby ally; the log carries its apply, stack, and remove events
/// but no unbuffed comparison, so the measure is how long it held and how high it stacked. Diamond's other
/// ranks raise primary attribute and armor, reduce incoming magic damage, and shorten relic cooldowns, which
/// are unmodelled while relic abilities are.
/// </summary>
public sealed partial class DiamondGemAnalyzer(Lazy<ThroughputTracker> throughputTracker)
    : Analyzer, IGemAnalyzer
{
    /// <summary>Grants critical strike, haste, expertise, and spirit, stacking once per nearby ally.</summary>
    public static readonly GemTrait HarmoniousSoul = new(
        Items.HarmoniousSoul, GemRankPower.Rank1,
        Items.HarmoniousSoulII, GemRankPower.Rank6);

    private int? _openedAt;
    private int _stacks;
    private int _lastStackChange;
    private long _stackMsIntegral;

    /// <inheritdoc/>
    public GemType Gem => GemType.Diamond;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Diamond;

    /// <inheritdoc/>
    public ThroughputTracker Throughput => throughputTracker.Value;

    /// <summary>Total time Harmonious Soul was active over the fight, in milliseconds.</summary>
    public int HarmoniousSoulUptimeMs { get; private set; }

    /// <summary>Harmonious Soul's share of the fight, as a fraction.</summary>
    public double HarmoniousSoulUptime =>
        Throughput.FightDurationMs > 0 ? (double)HarmoniousSoulUptimeMs / Throughput.FightDurationMs : 0;

    /// <summary>Time-weighted mean stack count while Harmonious Soul was active.</summary>
    public double HarmoniousSoulAverageStacks =>
        HarmoniousSoulUptimeMs > 0 ? (double)_stackMsIntegral / HarmoniousSoulUptimeMs : 0;

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApply(ApplyBuffEvent buffEvent)
    {
        if (Matches(buffEvent.Ability.FSLID))
            Open(buffEvent.Timestamp, stacks: 1);
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player)]
    private void OnApplyStack(ApplyBuffStackEvent buffEvent) => ChangeStacks(buffEvent, buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player)]
    private void OnRemoveStack(RemoveBuffStackEvent buffEvent) => ChangeStacks(buffEvent, buffEvent.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemove(RemoveBuffEvent buffEvent)
    {
        if (Matches(buffEvent.Ability.FSLID))
            Close(buffEvent.Timestamp);
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEndEvent) => Close(fightEndEvent.Timestamp);

    private void ChangeStacks(BuffEvent buffEvent, int stacks)
    {
        if (!Matches(buffEvent.Ability.FSLID)) return;

        if (_openedAt is null)
        {
            Open(buffEvent.Timestamp, stacks);
            return;
        }

        AccrueStackTime(buffEvent.Timestamp);
        _stacks = stacks;
    }

    private void Open(int timestamp, int stacks)
    {
        Close(timestamp);

        _openedAt = timestamp;
        _lastStackChange = timestamp;
        _stacks = stacks;
    }

    private void Close(int timestamp)
    {
        if (_openedAt is not int openedAt) return;

        AccrueStackTime(timestamp);
        HarmoniousSoulUptimeMs += timestamp - openedAt;
        _openedAt = null;
        _stacks = 0;
    }

    private void AccrueStackTime(int timestamp)
    {
        _stackMsIntegral += (long)_stacks * (timestamp - _lastStackChange);
        _lastStackChange = timestamp;
    }

    private static bool Matches(FSLID fslid) =>
        fslid == Items.HarmoniousSoul.FSLID || fslid == Items.HarmoniousSoulII.FSLID;

    /// <inheritdoc/>
    public override Type? StatisticsComponentType =>
        HarmoniousSoul.IsUnlocked(GemPower) ? typeof(DiamondGemStatistics) : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}

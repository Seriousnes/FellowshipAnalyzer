using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Sapphire gem contributed. Ancestral Surge is the trait the log accounts for,
/// a primary attribute buff active during Heroism; the log carries its apply and remove events but no
/// unbuffed comparison, so the measure is the time it was active. Sapphire's other ranks raise spirit and
/// extend Heroism.
/// </summary>
public sealed partial class SapphireGemAnalyzer(Lazy<ThroughputTracker> throughputTracker)
    : Analyzer, IGemAnalyzer
{
    /// <summary>Raises the player's primary attribute while Heroism is active.</summary>
    public static readonly GemTrait AncestralSurge = new(
        Items.AncestralSurge, GemRankPower.Rank1,
        Items.AncestralSurgeII, GemRankPower.Rank6);

    private int? _openedAt;

    /// <inheritdoc/>
    public GemType Gem => GemType.Sapphire;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Sapphire;

    /// <inheritdoc/>
    public ThroughputTracker Throughput => throughputTracker.Value;

    /// <summary>Total time Ancestral Surge was active over the fight, in milliseconds.</summary>
    public int AncestralSurgeUptimeMs { get; private set; }

    /// <summary>Ancestral Surge's share of the fight, as a fraction.</summary>
    public double AncestralSurgeUptime =>
        Throughput.FightDurationMs > 0 ? (double)AncestralSurgeUptimeMs / Throughput.FightDurationMs : 0;

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApply(ApplyBuffEvent buffEvent)
    {
        if (Matches(buffEvent.Ability.FSLID))
            _openedAt ??= buffEvent.Timestamp;
    }

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemove(RemoveBuffEvent buffEvent)
    {
        if (Matches(buffEvent.Ability.FSLID))
            Close(buffEvent.Timestamp);
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEndEvent) => Close(fightEndEvent.Timestamp);

    private void Close(int timestamp)
    {
        if (_openedAt is not int openedAt) return;

        AncestralSurgeUptimeMs += timestamp - openedAt;
        _openedAt = null;
    }

    private static bool Matches(FSLID fslid) =>
        fslid == Items.AncestralSurge.FSLID || fslid == Items.AncestralSurgeII.FSLID;

    /// <inheritdoc/>
    public override Type? StatisticsComponentType =>
        AncestralSurge.IsUnlocked(GemPower) ? typeof(SapphireGemStatistics) : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}

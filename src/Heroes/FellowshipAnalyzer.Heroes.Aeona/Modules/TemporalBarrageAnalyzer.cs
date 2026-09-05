using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>What a Temporal Barrage channel was aimed at.</summary>
public enum BarrageTarget
{
    /// <summary>The channel produced no bolt.</summary>
    Unknown,

    /// <summary>The channel damaged an enemy.</summary>
    Enemy,

    /// <summary>The channel healed an ally and damaged nobody.</summary>
    Ally,
}

/// <summary>One Temporal Barrage channel and everything its bolts produced.</summary>
public sealed record BarrageChannel
{
    /// <summary>When the channel started.</summary>
    public required int Start { get; init; }

    /// <summary>
    /// When the channel finished: the later of the channel-end event and the last bolt, falling back to
    /// <see cref="Start"/> for a channel that produced none.
    /// </summary>
    public required int End { get; init; }

    /// <summary>The instant each bolt struck, in order.</summary>
    public required IReadOnlyList<int> BoltTimestamps { get; init; }

    /// <summary>Damage the channel's bolts dealt.</summary>
    public required long Damage { get; init; }

    /// <summary>Effective healing the channel's bolts did.</summary>
    public required long HealEffective { get; init; }

    /// <summary>Healing the channel's bolts overhealed.</summary>
    public required long Overheal { get; init; }

    /// <summary>The enemies the channel damaged, in the order they were first struck.</summary>
    public required IReadOnlyList<int> DamageTargets { get; init; }

    /// <summary>The allies the channel healed, in the order they were first healed.</summary>
    public required IReadOnlyList<int> HealTargets { get; init; }

    /// <summary>What the channel was aimed at.</summary>
    public required BarrageTarget Target { get; init; }

    /// <summary>
    /// The ally that took the most of the channel's bolts. <see langword="null"/> for a channel that
    /// healed nobody.
    /// </summary>
    public required int? PrimaryHealTargetId { get; init; }

    /// <summary>
    /// The Stagger cleared off <see cref="PrimaryHealTargetId"/> across the channel, in hit points.
    /// </summary>
    public required int? StaggerCleared { get; init; }

    /// <summary>Whether Fleeting Hour was active when the channel started.</summary>
    public required bool FleetingHourActiveAtStart { get; init; }

    /// <summary>Bolts that struck while Fleeting Hour was active.</summary>
    public required int BoltsWhileFleetingHourActive { get; init; }

    /// <summary>Bolts that struck while Fleeting Hour was not active.</summary>
    public required int BoltsWhileFleetingHourInactive { get; init; }

    /// <summary>The Fleeting Hour duration Temporal Shift added over this channel, in milliseconds.</summary>
    public required int FleetingHourDurationExtendedMs { get; init; }

    /// <summary>The Fleeting Hour cooldown Temporal Shift reduced over this channel, in milliseconds.</summary>
    public required int FleetingHourCooldownReducedMs { get; init; }

    /// <summary>Bolts the channel produced.</summary>
    public int Bolts => BoltTimestamps.Count;

    /// <summary>How long the channel ran, in milliseconds.</summary>
    public int DurationMs => End - Start;

    /// <summary>Effective healing plus overheal.</summary>
    public long HealTotal => HealEffective + Overheal;
}

/// <summary>
/// Every Temporal Barrage channel across the dungeon, with the damage dealt, healing done and Stagger
/// cleared by each, and the Fleeting Hour duration Temporal Shift added and cooldown it reduced.
/// </summary>
/// <remarks>
/// <para>
/// A channel opens on the channel-start event and takes every Temporal Barrage damage and heal record
/// until the next one opens. A channel-end event closes the channel currently accumulating. Damage and
/// healing share a bolt's timestamp, so a bolt is one instant rather than one record.
/// </para>
/// </remarks>
[Dependency<FleetingHourAnalyzer>]
[Dependency<SpellUsable>]
[Dependency<StaggerTracker>]
public sealed partial class TemporalBarrageAnalyzer : Analyzer
{
    /// <summary>
    /// The Fleeting Hour benefit each Temporal Barrage bolt gives with Temporal Shift, in milliseconds.
    /// </summary>
    public const int TemporalShiftBenefitMs = 300;

    /// <summary>How far after a channel the Stagger bracket may close.</summary>
    public const int StaggerBracketToleranceMs = 2_000;

    private readonly List<ChannelBuilder> _builders = [];

    private ChannelBuilder? _current;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>Every Temporal Barrage channel, in the order they were started.</summary>
    public IReadOnlyList<BarrageChannel> Channels => field ??= [.. _builders.Select(Project)];

    /// <summary>Whether the player took Temporal Shift.</summary>
    public bool TemporalShiftTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.TemporalShift);

    /// <summary>Whether the player took Paradoxical Twist.</summary>
    public bool ParadoxicalTwistTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.ParadoxicalTwist);

    /// <summary>Damage every channel's bolts dealt.</summary>
    public long TotalDamage => Channels.Sum(channel => channel.Damage);

    /// <summary>Damage dealt by channels started while Fleeting Hour was active.</summary>
    public long DamageUnderFleetingHour =>
        Channels.Where(channel => channel.FleetingHourActiveAtStart).Sum(channel => channel.Damage);

    /// <summary>Effective healing every channel's bolts did.</summary>
    public long TotalHealEffective => Channels.Sum(channel => channel.HealEffective);

    /// <summary>Healing every channel's bolts overhealed.</summary>
    public long TotalOverheal => Channels.Sum(channel => channel.Overheal);

    /// <summary>Bolts struck across every channel.</summary>
    public int TotalBolts => Channels.Sum(channel => channel.Bolts);

    /// <summary>Channels aimed at an enemy.</summary>
    public int EnemyChannels => Channels.Count(channel => channel.Target == BarrageTarget.Enemy);

    /// <summary>Channels aimed at an ally.</summary>
    public int AllyChannels => Channels.Count(channel => channel.Target == BarrageTarget.Ally);

    /// <summary>Channels started while Fleeting Hour was active.</summary>
    public int ChannelsUnderFleetingHour => Channels.Count(channel => channel.FleetingHourActiveAtStart);

    /// <summary>Fleeting Hour duration Temporal Shift added across every channel, in milliseconds.</summary>
    public int FleetingHourDurationExtendedMs =>
        Channels.Sum(channel => channel.FleetingHourDurationExtendedMs);

    /// <summary>Fleeting Hour cooldown Temporal Shift reduced across every channel, in milliseconds.</summary>
    public int FleetingHourCooldownReducedMs =>
        Channels.Sum(channel => channel.FleetingHourCooldownReducedMs);

    /// <summary>Bolts that struck while Fleeting Hour was not active, across every channel.</summary>
    public int BoltsWhileFleetingHourInactive =>
        Channels.Sum(channel => channel.BoltsWhileFleetingHourInactive);

    /// <summary>Bolts that struck while Fleeting Hour was active, across every channel.</summary>
    public int BoltsWhileFleetingHourActive =>
        Channels.Sum(channel => channel.BoltsWhileFleetingHourActive);

    /// <summary>Stagger cleared off the ally target across every ally-aimed channel, in hit points.</summary>
    public int StaggerCleared => Channels.Sum(channel => channel.StaggerCleared ?? 0);

    [On<BeginChannelEvent>(By = Actor.Player, Spell = nameof(Spells.TemporalBarrage))]
    private void OnChannelBegin(BeginChannelEvent e)
    {
        _current = new ChannelBuilder(e.Timestamp);
        _builders.Add(_current);
    }

    [On<EndChannelEvent>(By = Actor.Player, Spell = nameof(Spells.TemporalBarrage))]
    private void OnChannelEnd(EndChannelEvent e) => _current?.ReportEnd(e.Timestamp);

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.TemporalBarrage))]
    private void OnBarrageDamage(DamageEvent e)
    {
        if (_current is not { } channel) return;

        channel.AddDamage(e.Amount, e.TargetId);
        RecordBolt(channel, e.Timestamp);
    }

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.TemporalBarrage))]
    private void OnBarrageHeal(HealEvent e)
    {
        if (_current is not { } channel) return;

        channel.AddHeal(e.Amount, e.Overheal ?? 0, e.TargetId);
        RecordBolt(channel, e.Timestamp);
    }

    private void RecordBolt(ChannelBuilder channel, int timestamp)
    {
        if (!channel.AddBolt(timestamp)) return;
        if (!TemporalShiftTaken || FleetingHourAnalyzer.IsBuffActiveAt(timestamp)) return;

        channel.AddCooldownReduction(
            SpellUsable.ReduceCooldown(Spells.FleetingHour.FSLID, TemporalShiftBenefitMs, timestamp).Effective);
    }

    private BarrageChannel Project(ChannelBuilder builder)
    {
        var target = builder.DamageTargets.Count > 0
            ? BarrageTarget.Enemy
            : builder.HealTargets.Count > 0 ? BarrageTarget.Ally : BarrageTarget.Unknown;

        var primaryHealTarget = builder.PrimaryHealTarget();
        var end = builder.End;
        var active = TemporalShiftTaken
            ? builder.BoltTimestamps.Count(FleetingHourAnalyzer.IsBuffActiveAt)
            : 0;

        return new BarrageChannel
        {
            Start = builder.Start,
            End = end,
            BoltTimestamps = builder.BoltTimestamps,
            Damage = builder.Damage,
            HealEffective = builder.HealEffective,
            Overheal = builder.Overheal,
            DamageTargets = builder.DamageTargets,
            HealTargets = builder.HealTargets,
            Target = target,
            PrimaryHealTargetId = primaryHealTarget,
            StaggerCleared = target == BarrageTarget.Ally && primaryHealTarget is { } ally
                ? MeasureStaggerCleared(ally, builder.Start, end)
                : null,
            FleetingHourActiveAtStart = FleetingHourAnalyzer.IsBuffActiveAt(builder.Start),
            BoltsWhileFleetingHourActive = active,
            BoltsWhileFleetingHourInactive = builder.BoltTimestamps.Count - active,
            FleetingHourDurationExtendedMs = active * TemporalShiftBenefitMs,
            FleetingHourCooldownReducedMs = builder.CooldownReducedMs,
        };
    }

    private int? MeasureStaggerCleared(int unitId, int start, int end) =>
        StaggerTracker.MeasureCleanseBetween(unitId, start, end, StaggerBracketToleranceMs) is
            { HasInterveningEvent: false } cleanse
            ? cleanse.ClearedAmount
            : null;

    private sealed class ChannelBuilder(int start)
    {
        private readonly List<int> _boltTimestamps = [];
        private readonly List<int> _damageTargets = [];
        private readonly List<int> _healTargets = [];
        private readonly Dictionary<int, int> _healCounts = [];

        public int Start { get; } = start;

        public int End { get; private set; } = start;

        public long Damage { get; private set; }

        public long HealEffective { get; private set; }

        public long Overheal { get; private set; }

        public int CooldownReducedMs { get; private set; }

        public IReadOnlyList<int> BoltTimestamps => _boltTimestamps;

        public IReadOnlyList<int> DamageTargets => _damageTargets;

        public IReadOnlyList<int> HealTargets => _healTargets;

        public bool AddBolt(int timestamp)
        {
            if (timestamp > End)
                End = timestamp;

            if (_boltTimestamps.Count > 0 && _boltTimestamps[^1] == timestamp) return false;

            _boltTimestamps.Add(timestamp);
            return true;
        }

        public void ReportEnd(int timestamp) => End = Math.Max(End, timestamp);

        public void AddDamage(long amount, int targetId)
        {
            Damage += amount;
            if (!_damageTargets.Contains(targetId))
                _damageTargets.Add(targetId);
        }

        public void AddHeal(long effective, long overheal, int targetId)
        {
            HealEffective += effective;
            Overheal += overheal;
            if (!_healTargets.Contains(targetId))
                _healTargets.Add(targetId);

            _healCounts[targetId] = _healCounts.GetValueOrDefault(targetId) + 1;
        }

        public void AddCooldownReduction(int effectiveMs) => CooldownReducedMs += effectiveMs;

        public int? PrimaryHealTarget()
        {
            int? best = null;
            var bestCount = 0;

            foreach (var targetId in _healTargets)
            {
                var count = _healCounts.GetValueOrDefault(targetId);
                if (count <= bestCount) continue;

                best = targetId;
                bestCount = count;
            }

            return best;
        }
    }
}

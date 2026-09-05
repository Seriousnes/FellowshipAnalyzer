using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>What a Temporal Barrage channel was aimed at, read from what its bolts produced.</summary>
public enum BarrageTarget
{
    /// <summary>The channel produced no bolt, so the log names nothing to read the target from.</summary>
    Unknown,

    /// <summary>The channel damaged an enemy. Its heals are the Deferred Fate relay, not an ally target.</summary>
    Enemy,

    /// <summary>The channel healed an ally and damaged nobody.</summary>
    Ally,
}

/// <summary>
/// One Temporal Barrage channel and everything its bolts produced.
/// </summary>
public sealed record BarrageChannel
{
    /// <summary>When the channel started.</summary>
    public required int Start { get; init; }

    /// <summary>
    /// When the channel finished: the later of the log's channel-end timestamp and the last bolt,
    /// falling back to <see cref="Start"/> on a channel that landed nothing. Fellowship credits a
    /// bolt up to two thirds of a second after the channel-end it reports, so taking the reported
    /// end alone would close a channel ahead of its own bolts.
    /// </summary>
    public required int End { get; init; }

    /// <summary>
    /// Whether the log closed this channel with a channel-end event. Fellowship emits one for a
    /// small minority of channels, so a false reading means <see cref="End"/> rests on the last
    /// bolt alone.
    /// </summary>
    public required bool EndReported { get; init; }

    /// <summary>The instant each bolt landed, in order.</summary>
    public required IReadOnlyList<int> TickTimestamps { get; init; }

    /// <summary>Damage the channel's bolts dealt.</summary>
    public required long Damage { get; init; }

    /// <summary>Effective healing the channel's bolts did.</summary>
    public required long HealEffective { get; init; }

    /// <summary>Healing the channel's bolts overhealed.</summary>
    public required long Overheal { get; init; }

    /// <summary>The enemies the channel damaged, in the order they were first hit.</summary>
    public required IReadOnlyList<int> DamageTargets { get; init; }

    /// <summary>The allies the channel healed, in the order they were first healed.</summary>
    public required IReadOnlyList<int> HealTargets { get; init; }

    /// <summary>What the channel was aimed at.</summary>
    public required BarrageTarget Target { get; init; }

    /// <summary>
    /// The ally that took the most of the channel's bolts, which is the channel's target on an
    /// ally-aimed channel. <see langword="null"/> when the channel healed nobody.
    /// </summary>
    public required int? PrimaryHealTargetId { get; init; }

    /// <summary>
    /// The Stagger that came off <see cref="PrimaryHealTargetId"/> across the channel, in hit
    /// points, reconstructed from the readings either side of it. Measured on an ally-aimed channel
    /// alone, and <see langword="null"/> when either reading is missing or too far from the channel
    /// to bracket it. Negative when the ally took staggered damage during the channel.
    /// </summary>
    public required int? StaggerCleared { get; init; }

    /// <summary>When the reading before the channel was taken, or <see langword="null"/> when there is none.</summary>
    public required int? StaggerPreTimestamp { get; init; }

    /// <summary>When the reading after the channel was taken, or <see langword="null"/> when there is none.</summary>
    public required int? StaggerPostTimestamp { get; init; }

    /// <summary>Whether Fleeting Hour was active when the channel started, which is what Paradoxical Twist empowers on.</summary>
    public required bool FleetingHourActiveAtStart { get; init; }

    /// <summary>Bolts that landed while Fleeting Hour was active, which is what Temporal Shift extends on.</summary>
    public required int TicksWhileFleetingHourActive { get; init; }

    /// <summary>Bolts that landed while Fleeting Hour was inactive, which is what Temporal Shift reduces the cooldown on.</summary>
    public required int TicksWhileFleetingHourInactive { get; init; }

    /// <summary>
    /// The Fleeting Hour duration Temporal Shift added over this channel, in milliseconds, derived
    /// from <see cref="TicksWhileFleetingHourActive"/> and the talent's stated benefit. Zero when
    /// the talent is not taken.
    /// </summary>
    public required int FleetingHourDurationExtensionMs { get; init; }

    /// <summary>
    /// The Fleeting Hour cooldown reduction Temporal Shift generated over this channel, split into
    /// what it generated and what shortened a running cooldown. The split comes from the tool's
    /// cooldown model, which does not carry Fleeting Hour's hold while active.
    /// </summary>
    public required CooldownReductionResult FleetingHourCooldownReduction { get; init; }

    /// <summary>Bolts the channel landed.</summary>
    public int Ticks => TickTimestamps.Count;

    /// <summary>How long the channel ran, in milliseconds.</summary>
    public int DurationMs => End - Start;

    /// <summary>Healing the channel's bolts did before overhealing was taken off.</summary>
    public long HealTotal => HealEffective + Overheal;

    /// <summary>Share of the channel's healing that landed, from 0 to 1.</summary>
    public double HealEfficiency => HealTotal <= 0 ? 0 : (double)HealEffective / HealTotal;
}

/// <summary>
/// Every Temporal Barrage channel in the pull, with the damage, healing and Stagger each one moved,
/// and the Fleeting Hour benefit Temporal Shift drew from its bolts.
/// </summary>
/// <remarks>
/// <para>
/// A channel opens on the log's channel-start event and takes every Temporal Barrage damage and heal
/// record until the next one opens. Fellowship reports a channel-end event for fewer than a tenth of
/// channels and attributes it to the channel open at the time, so a channel-end closes the channel
/// currently accumulating. Damage and healing share a bolt's timestamp, so a bolt is one instant
/// rather than one record.
/// </para>
/// <para>
/// Codex <c>ability 1926</c> states that 100% of the damage Temporal Barrage deals heals an ally
/// inside the Aura of Deferred Fate, so healing is no evidence of an ally-aimed channel; damage is
/// what separates the two, and <see cref="BarrageTarget"/> reads it that way.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<FleetingHourAnalyzer>]
[Dependency<SpellUsable>]
[Dependency<StaggerTracker>]
public sealed partial class TemporalBarrageAnalyzer : Analyzer
{
    /// <summary>
    /// The Fleeting Hour benefit each Temporal Barrage bolt carries with Temporal Shift, in
    /// milliseconds. Codex <c>talent 590</c>: "While Fleeting Hour is active, its duration is
    /// extended by 0.3 seconds. While Fleeting Hour is on cooldown, its cooldown is reduced by 0.3
    /// seconds."
    /// </summary>
    public const int TemporalShiftBenefitMs = 300;

    /// <summary>
    /// How far outside a channel a Stagger reading may fall and still bracket it in
    /// <see cref="BarrageChannel.StaggerCleared"/>.
    /// </summary>
    public const int StaggerBracketToleranceMs = 2_000;

    private readonly List<ChannelBuilder> _builders = [];

    private ChannelBuilder? _current;

    /// <summary>
    /// The bolts a full Temporal Barrage channel lands before haste, taken from the spell registry's
    /// channel duration and tick interval. Haste shortens the interval without shortening the
    /// channel, so a channel that ran its course meets or exceeds this and one below it was cut short.
    /// </summary>
    public static int ExpectedBolts
    {
        get
        {
            var duration = Spells.TemporalBarrage.ChannelDuration ?? 0;
            var interval = Spells.TemporalBarrage.ChannelTickInterval ?? 0;
            return interval <= 0 ? 0 : (int)Math.Round(duration / interval);
        }
    }

    /// <summary>Every Temporal Barrage channel in the pull, in the order they were started.</summary>
    public IReadOnlyList<BarrageChannel> Channels => field ??= [.. _builders.Select(Project)];

    /// <summary>Channels that landed at least <see cref="ExpectedBolts"/> bolts.</summary>
    public int CompletedChannels => Channels.Count(channel => channel.Ticks >= ExpectedBolts);

    /// <summary>Whether the player took Temporal Shift.</summary>
    public bool TemporalShiftTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.TemporalShift);

    /// <summary>Whether the player took Paradoxical Twist, which empowers a Barrage cast under Fleeting Hour.</summary>
    public bool ParadoxicalTwistTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.ParadoxicalTwist);

    /// <summary>Damage every channel's bolts dealt.</summary>
    public long TotalDamage => Channels.Sum(channel => channel.Damage);

    /// <summary>Effective healing every channel's bolts did.</summary>
    public long TotalHealEffective => Channels.Sum(channel => channel.HealEffective);

    /// <summary>Healing every channel's bolts overhealed.</summary>
    public long TotalOverheal => Channels.Sum(channel => channel.Overheal);

    /// <summary>Bolts landed across every channel.</summary>
    public int TotalTicks => Channels.Sum(channel => channel.Ticks);

    /// <summary>Channels aimed at an enemy.</summary>
    public int EnemyChannels => Channels.Count(channel => channel.Target == BarrageTarget.Enemy);

    /// <summary>Channels aimed at an ally.</summary>
    public int AllyChannels => Channels.Count(channel => channel.Target == BarrageTarget.Ally);

    /// <summary>Channels that landed no bolt, so there is nothing to read their target from.</summary>
    public int ChannelsWithoutBolts => Channels.Count(channel => channel.Target == BarrageTarget.Unknown);

    /// <summary>Channels started while Fleeting Hour was active, which Paradoxical Twist empowers.</summary>
    public int ChannelsUnderFleetingHour => Channels.Count(channel => channel.FleetingHourActiveAtStart);

    /// <summary>Fleeting Hour duration Temporal Shift added across every channel, in milliseconds.</summary>
    public int FleetingHourDurationExtensionMs =>
        Channels.Sum(channel => channel.FleetingHourDurationExtensionMs);

    /// <summary>Fleeting Hour cooldown reduction Temporal Shift generated across every channel.</summary>
    public CooldownReductionResult FleetingHourCooldownReduction =>
        Channels.Aggregate(new CooldownReductionResult(), (running, channel) => running + channel.FleetingHourCooldownReduction);

    /// <summary>Stagger taken off the ally target across every ally-aimed channel the readings bracket, in hit points.</summary>
    public int StaggerCleared => Channels.Sum(channel => channel.StaggerCleared ?? 0);

    /// <summary>Ally-aimed channels whose Stagger readings bracket the channel, so their cleanse could be measured.</summary>
    public int MeasuredStaggerChannels => Channels.Count(channel => channel.StaggerCleared is not null);

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
        RecordTick(channel, e.Timestamp);
    }

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.TemporalBarrage))]
    private void OnBarrageHeal(HealEvent e)
    {
        if (_current is not { } channel) return;

        channel.AddHeal(e.Amount, e.Overheal ?? 0, e.TargetId);
        RecordTick(channel, e.Timestamp);
    }

    private void RecordTick(ChannelBuilder channel, int timestamp)
    {
        if (!channel.AddTick(timestamp)) return;
        if (!TemporalShiftTaken || FleetingHourAnalyzer.IsBuffActiveAt(timestamp)) return;

        channel.AddCooldownReduction(
            SpellUsable.ReduceCooldown(Spells.FleetingHour.FSLID, TemporalShiftBenefitMs, timestamp));
    }

    private BarrageChannel Project(ChannelBuilder builder)
    {
        var target = builder.DamageTargets.Count > 0
            ? BarrageTarget.Enemy
            : builder.HealTargets.Count > 0 ? BarrageTarget.Ally : BarrageTarget.Unknown;

        var primaryHealTarget = builder.PrimaryHealTarget();
        var end = builder.End;
        var active = TemporalShiftTaken
            ? builder.TickTimestamps.Count(FleetingHourAnalyzer.IsBuffActiveAt)
            : 0;

        var (cleared, preTimestamp, postTimestamp) = target == BarrageTarget.Ally && primaryHealTarget is { } ally
            ? MeasureStagger(ally, builder.Start, end)
            : (null, null, null);

        return new BarrageChannel
        {
            Start = builder.Start,
            End = end,
            EndReported = builder.EndReported,
            TickTimestamps = builder.TickTimestamps,
            Damage = builder.Damage,
            HealEffective = builder.HealEffective,
            Overheal = builder.Overheal,
            DamageTargets = builder.DamageTargets,
            HealTargets = builder.HealTargets,
            Target = target,
            PrimaryHealTargetId = primaryHealTarget,
            StaggerCleared = cleared,
            StaggerPreTimestamp = preTimestamp,
            StaggerPostTimestamp = postTimestamp,
            FleetingHourActiveAtStart = FleetingHourAnalyzer.IsBuffActiveAt(builder.Start),
            TicksWhileFleetingHourActive = active,
            TicksWhileFleetingHourInactive = builder.TickTimestamps.Count - active,
            FleetingHourDurationExtensionMs = active * TemporalShiftBenefitMs,
            FleetingHourCooldownReduction = builder.CooldownReduction,
        };
    }

    private (int? Cleared, int? PreTimestamp, int? PostTimestamp) MeasureStagger(int unitId, int start, int end)
    {
        var pre = StaggerTracker.LatestBefore(unitId, start);
        var post = StaggerTracker.EarliestAfter(unitId, end);

        if (pre is null || post is null) return (null, pre?.Timestamp, post?.Timestamp);
        if (start - pre.Timestamp > StaggerBracketToleranceMs) return (null, pre.Timestamp, post.Timestamp);
        if (post.Timestamp - end > StaggerBracketToleranceMs) return (null, pre.Timestamp, post.Timestamp);

        return (pre.Amount - post.Amount, pre.Timestamp, post.Timestamp);
    }

    private sealed class ChannelBuilder(int start)
    {
        private readonly List<int> _tickTimestamps = [];
        private readonly List<int> _damageTargets = [];
        private readonly List<int> _healTargets = [];
        private readonly Dictionary<int, int> _healCounts = [];

        public int Start { get; } = start;

        public int End { get; private set; } = start;

        public bool EndReported { get; private set; }

        public long Damage { get; private set; }

        public long HealEffective { get; private set; }

        public long Overheal { get; private set; }

        public CooldownReductionResult CooldownReduction { get; private set; }

        public IReadOnlyList<int> TickTimestamps => _tickTimestamps;

        public IReadOnlyList<int> DamageTargets => _damageTargets;

        public IReadOnlyList<int> HealTargets => _healTargets;

        public bool AddTick(int timestamp)
        {
            if (timestamp > End)
                End = timestamp;

            if (_tickTimestamps.Count > 0 && _tickTimestamps[^1] == timestamp) return false;

            _tickTimestamps.Add(timestamp);
            return true;
        }

        public void ReportEnd(int timestamp)
        {
            End = Math.Max(End, timestamp);
            EndReported = true;
        }

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

        public void AddCooldownReduction(CooldownReductionResult result) => CooldownReduction += result;

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

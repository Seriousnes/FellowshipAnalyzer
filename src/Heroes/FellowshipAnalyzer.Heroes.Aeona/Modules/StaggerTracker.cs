using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;
using SpellKind = FellowshipAnalyzer.Core.Common.Spells.SpellKind;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>A unit's Stagger pool at one instant, from the <see cref="ActorResources"/> block on a log event.</summary>
/// <param name="Timestamp">The instant.</param>
/// <param name="Amount">The Stagger pending on the unit, in hit points. <see cref="Analysis.Normalizers.ResourceNormalizer"/> has already divided the raw log value by 100.</param>
/// <param name="Max">The pool's cap as the log reports it. Fellowship sends the no-maximum sentinel for Stagger, which normalization leaves as <c>-1</c>, so the pool is uncapped and <paramref name="Amount"/> is an absolute figure rather than a percentage.</param>
/// <param name="HitPoints">The unit's current hit points at the same instant.</param>
/// <param name="MaxHitPoints">The unit's maximum hit points at the same instant.</param>
public sealed record StaggerSnapshot(int Timestamp, int Amount, int Max, long HitPoints, long MaxHitPoints);

/// <summary>
/// One Amend Fate or Restore Continuity cast by the player, with the targets its heals reached.
/// </summary>
/// <param name="timestamp">When the cast completed.</param>
/// <param name="ability">The FSLID of the ability cast, either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
/// <param name="targetId">The target id on the cast event. Both cleanses log no named target, so this reads <c>-1</c> on real casts; read <see cref="HealTargets"/> instead.</param>
public sealed class CleanseCast(int timestamp, FSLID ability, int targetId)
{
    private readonly List<int> _healTargets = [];

    /// <summary>When the cast completed.</summary>
    public int Timestamp { get; } = timestamp;

    /// <summary>The ability cast, either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</summary>
    public FSLID Ability { get; } = ability;

    /// <summary>The target id on the cast event, which is <c>-1</c> for both cleanses.</summary>
    public int TargetId { get; } = targetId;

    /// <summary>
    /// The units this cast's heals reached, in the order they were healed, attributed by taking every
    /// heal of the same ability up to the player's next cast of it.
    /// </summary>
    public IReadOnlyList<int> HealTargets => _healTargets;

    internal void AddHealTarget(int unitId)
    {
        if (!_healTargets.Contains(unitId))
            _healTargets.Add(unitId);
    }
}

/// <summary>
/// The Stagger a cleanse cast cleared off one unit, reconstructed from the unit's Stagger pool either
/// side of the cast.
/// </summary>
/// <param name="UnitId">The unit whose pool was measured.</param>
/// <param name="CastTimestamp">The cast the measurement brackets.</param>
/// <param name="PreTimestamp">When the pool was at <paramref name="PreAmount"/>. The further this sits ahead of <paramref name="CastTimestamp"/>, the staler it is.</param>
/// <param name="PreAmount">The unit's Stagger at <paramref name="PreTimestamp"/>, in hit points.</param>
/// <param name="PostTimestamp">When the pool was at <paramref name="PostAmount"/>.</param>
/// <param name="PostAmount">The unit's Stagger at <paramref name="PostTimestamp"/>, in hit points.</param>
/// <param name="ClearedAmount">The pool's fall across the bracket, in hit points. Negative when the pool grew, which happens when the unit took staggered damage inside the bracket.</param>
/// <param name="InterveningTickCount">Stagger drain ticks on the unit after <paramref name="PreTimestamp"/> and up to <paramref name="PostTimestamp"/>, each of which drained the pool independently of the cleanse.</param>
/// <param name="InterveningCleanseCount">Other Amend Fate and Restore Continuity casts by the player inside the same bracket, each of which may have cleared part of <paramref name="ClearedAmount"/>.</param>
public sealed record StaggerCleanse(
    int UnitId,
    int CastTimestamp,
    int PreTimestamp,
    int PreAmount,
    int PostTimestamp,
    int PostAmount,
    int ClearedAmount,
    int InterveningTickCount,
    int InterveningCleanseCount)
{
    /// <summary>
    /// Whether something other than the measured cast moved the pool inside the bracket, which makes
    /// <see cref="ClearedAmount"/> an upper bound on what the cast cleared rather than the figure itself.
    /// </summary>
    public bool HasInterveningEvent => InterveningTickCount > 0 || InterveningCleanseCount > 0;
}

/// <summary>
/// Reconstructs every party member's Stagger pool across the dungeon.
/// <para>
/// Stagger is <see cref="ResourceTypes.Stagger"/> inside the <see cref="Event.SourceResources"/> and
/// <see cref="Event.TargetResources"/> blocks on events, and no Core module aggregates resources for a
/// unit other than the analyzed player, so this module harvests those blocks from an unfiltered
/// <c>[On&lt;Event&gt;]</c> handler the way <c>ResourceTracker</c> does. A unit with a Stagger entry is a
/// party member by construction, so no actor table is needed to decide what to keep.
/// </para>
/// <para>
/// The amount a cleanse cleared is the fall in the target's Stagger across the cast.
/// <see cref="MeasureCleanse"/> reconstructs it and reports what else moved the pool inside the bracket.
/// </para>
/// </summary>
[Dependency<ChronaTracker>]
public sealed partial class StaggerTracker : Analyzer
{
    /// <summary>
    /// The effect the Stagger pool drains through, as periodic self-damage on the staggered unit roughly
    /// every three seconds. Codex <c>effect 2696</c>, named "Stagger"; the effect has no spell-registry
    /// member because it belongs to no hero's kit.
    /// </summary>
    private const int StaggerDrainEffectId = 2696;

    /// <summary>
    /// How far back a Stagger figure may sit and still describe the pool at the instant asked about.
    /// Anything older is withheld rather than given as a stale figure.
    /// </summary>
    public const int StaggerMaxAgeMs = 1000;

    /// <summary>How long after a cleanse cast a Stagger figure may fall and still close its bracket.</summary>
    public const int CleanseBracketWindowMs = 500;

    private static readonly FSLID StaggerDrain = FSLID.FromNative(SpellKind.Effect, StaggerDrainEffectId);

    private readonly Dictionary<int, List<StaggerSnapshot>> _snapshots = [];
    private readonly List<int> _trackedUnitIds = [];
    private readonly Dictionary<int, List<int>> _drainTicks = [];
    private readonly List<CleanseCast> _cleanseCasts = [];
    private readonly Dictionary<int, CleanseCast> _latestCleanseCastByAbility = [];
    private readonly Dictionary<int, List<int>> _deaths = [];

    /// <summary>Every unit with a Stagger pool, in the order each was first seen.</summary>
    public IReadOnlyList<int> TrackedUnitIds => _trackedUnitIds;

    /// <summary>
    /// The party's tank actor ids, resolved from the report's actor list by parsing each actor's hero
    /// and keeping those whose <see cref="HeroRole"/> is <see cref="HeroRole.Tank"/>. Empty when the
    /// report has no actor list.
    /// </summary>
    public IReadOnlyList<int> TankIds => field ??=
    [
        .. Owner.Actors
            .Where(actor => Hero.TryParse(actor.SubType, out var hero) && hero.Role == HeroRole.Tank)
            .Select(actor => actor.Id),
    ];

    /// <summary>
    /// The encounter tank's actor id, or <see langword="null"/> when the report names no tank. A party
    /// running two tanks reports the first; read <see cref="TankIds"/> for all of them.
    /// </summary>
    public int? TankId => TankIds.Count > 0 ? TankIds[0] : null;

    /// <summary>
    /// Every Amend Fate and Restore Continuity cast by the player, in cast order.
    /// </summary>
    public IReadOnlyList<CleanseCast> CleanseCasts => _cleanseCasts;

    /// <summary>
    /// Amend Fate and Restore Continuity casts across the report that could be bracketed, the count
    /// behind <see cref="StaggerCleansedTotal"/> and <see cref="AverageStaggerCleansedPerCast"/>.
    /// </summary>
    public int CleanseCastsMeasured => CleanseTotals().Casts;

    /// <summary>
    /// The Stagger those casts removed across the report, in hit points, summed over every ally each
    /// cast reached.
    /// </summary>
    public int StaggerCleansedTotal => CleanseTotals().Total;

    /// <summary>
    /// The Stagger a single Amend Fate or Restore Continuity cast removed across the report, in hit
    /// points, totalled over the allies the cast reached and averaged over
    /// <see cref="CleanseCastsMeasured"/>. <see langword="null"/> when no cast could be bracketed.
    /// </summary>
    public double? AverageStaggerCleansedPerCast
    {
        get
        {
            var (total, casts) = CleanseTotals();
            return casts == 0 ? null : (double)total / casts;
        }
    }

    /// <summary>The Stagger the party accumulated across the dungeon, in hit points.</summary>
    public int StaggerGenerated => StaggerGeneratedBetween(Owner.DungeonStartTime, Owner.DungeonEndTime);

    /// <summary>
    /// The mana Amend Fate and Restore Continuity generated across the dungeon.
    /// </summary>
    /// <remarks>
    /// The game data states no generation amount for either cleanse, so the pool's rise stands on its
    /// own and no share of it can be attributed to the mana cap.
    /// </remarks>
    public int ManaFromCleansing =>
        ManaFromCleansingBetween(Owner.DungeonStartTime, Owner.DungeonEndTime);

    /// <summary>
    /// <paramref name="unitId"/>'s Stagger pool over time, in chronological order with consecutive
    /// identical entries collapsed to the first of the run. Empty for a unit with no Stagger pool.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    public IReadOnlyList<StaggerSnapshot> SnapshotsFor(int unitId) =>
        _snapshots.TryGetValue(unitId, out var snapshots) ? snapshots : [];

    /// <summary>
    /// The timestamps of the Stagger drain ticks on <paramref name="unitId"/>, in chronological order.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    public IReadOnlyList<int> DrainTicksFor(int unitId) =>
        _drainTicks.TryGetValue(unitId, out var ticks) ? ticks : [];

    /// <summary>
    /// <paramref name="unitId"/>'s Stagger pool at the last entry strictly before
    /// <paramref name="timestamp"/>, or <see langword="null"/> when the unit has none that early.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to read back from.</param>
    public StaggerSnapshot? LatestBefore(int unitId, int timestamp)
    {
        if (!_snapshots.TryGetValue(unitId, out var snapshots)) return null;

        var index = FirstIndexAtOrAfter(snapshots, timestamp);
        return index > 0 ? snapshots[index - 1] : null;
    }

    /// <summary>
    /// <paramref name="unitId"/>'s Stagger pool at the first entry at or after
    /// <paramref name="timestamp"/>, or <see langword="null"/> when the unit has none that late. The
    /// boundary is inclusive, so an entry in the same millisecond as a cast counts as the entry after it,
    /// which is where Fellowship puts a cleanse's own heal.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to read forward from.</param>
    public StaggerSnapshot? EarliestAfter(int unitId, int timestamp)
    {
        if (!_snapshots.TryGetValue(unitId, out var snapshots)) return null;

        var index = FirstIndexAtOrAfter(snapshots, timestamp);
        return index < snapshots.Count ? snapshots[index] : null;
    }

    /// <summary>
    /// The Stagger pending on <paramref name="unitId"/> immediately before <paramref name="timestamp"/>,
    /// as a fraction of the unit's maximum hit points, or <see langword="null"/> when nothing precedes it.
    /// Stagger's reported maximum is Fellowship's no-maximum sentinel, so the pool is uncapped and the
    /// amount is an absolute hit-point figure; maximum hit points is the only meaningful scale, and the
    /// fraction exceeds 1 when a unit holds more Stagger than its maximum hit points.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to read back from.</param>
    public double? StaggerFractionOfMaxHp(int unitId, int timestamp)
    {
        if (LatestBefore(unitId, timestamp) is not { } snapshot) return null;
        if (MaxHitPointsOf(unitId, timestamp) is not { } maxHitPoints) return null;

        return (double)snapshot.Amount / maxHitPoints;
    }

    /// <summary>
    /// The same fraction as <see cref="StaggerFractionOfMaxHp(int, int)"/>, given only when the figure
    /// behind it sits within <paramref name="maxAgeMs"/> of <paramref name="timestamp"/>.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to read back from.</param>
    /// <param name="maxAgeMs">How far back the figure may sit.</param>
    public double? StaggerFractionOfMaxHp(int unitId, int timestamp, int maxAgeMs)
    {
        if (LatestBefore(unitId, timestamp) is not { } snapshot) return null;
        if (timestamp - snapshot.Timestamp > maxAgeMs) return null;
        if (MaxHitPointsOf(unitId, timestamp) is not { } maxHitPoints) return null;

        return (double)snapshot.Amount / maxHitPoints;
    }

    /// <summary>
    /// Whether <paramref name="unitId"/> was alive at <paramref name="timestamp"/>: true until the unit
    /// dies, and false from that death until the unit's next hit points above zero.
    /// </summary>
    /// <remarks>
    /// Core's <c>DeathTracker</c> records the analyzed player alone, so party deaths are reconstructed
    /// here from the unfiltered death stream and the hit points on every Stagger entry.
    /// </remarks>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant asked about.</param>
    public bool IsAlive(int unitId, int timestamp)
    {
        if (!_deaths.TryGetValue(unitId, out var deaths)) return true;

        var lastDeath = 0;
        var died = false;
        foreach (var death in deaths)
        {
            if (death > timestamp) break;
            lastDeath = death;
            died = true;
        }

        if (!died) return true;

        if (!_snapshots.TryGetValue(unitId, out var snapshots)) return false;

        var index = FirstIndexAtOrAfter(snapshots, lastDeath);
        for (var i = index; i < snapshots.Count && snapshots[i].Timestamp <= timestamp; i++)
        {
            if (snapshots[i].HitPoints > 0) return true;
        }

        return false;
    }

    /// <summary>
    /// The Stagger <paramref name="abilityId"/> removes, taken from the report itself as the median of
    /// every clean cast whose target still held Stagger afterwards, so the cast cleared its whole amount
    /// rather than emptying the pool. <see langword="null"/> when the report holds no such cast.
    /// </summary>
    /// <remarks>
    /// The median rather than an extreme: brackets in real reports catch drain ticks and incoming
    /// staggered damage, which pushes individual measurements well above and below the amount the ability
    /// removes.
    /// </remarks>
    /// <param name="abilityId">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
    public int? StaggerRemoved(FSLID abilityId)
    {
        var amounts = new List<int>();

        foreach (var cast in _cleanseCasts)
        {
            if (cast.Ability != abilityId) continue;

            foreach (var unitId in cast.HealTargets)
            {
                if (MeasureCleanse(unitId, cast.Timestamp, CleanseBracketWindowMs) is not { } cleanse) continue;
                if (cleanse.HasInterveningEvent || cleanse.ClearedAmount <= 0 || cleanse.PostAmount <= 0) continue;

                amounts.Add(cleanse.ClearedAmount);
            }
        }

        if (amounts.Count == 0) return null;

        amounts.Sort();
        return amounts[amounts.Count / 2];
    }

    /// <summary>
    /// Reconstructs how <paramref name="unitId"/>'s Stagger moved across the window from
    /// <paramref name="startTimestamp"/> to <paramref name="endTimestamp"/>, for a channel rather than a
    /// single cast. The bracket opens on the unit's last Stagger before the window and closes on its
    /// first from the window's end onwards, within <paramref name="toleranceMs"/> of it.
    /// </summary>
    /// <param name="unitId">The unit to measure.</param>
    /// <param name="startTimestamp">When the window opened.</param>
    /// <param name="endTimestamp">When the window closed.</param>
    /// <param name="toleranceMs">How long after the window the closing figure may fall and still close the bracket.</param>
    public StaggerCleanse? MeasureCleanseBetween(int unitId, int startTimestamp, int endTimestamp, int toleranceMs)
    {
        if (LatestBefore(unitId, startTimestamp) is not { } pre) return null;
        if (EarliestAfter(unitId, endTimestamp) is not { } post) return null;
        if (post.Timestamp > endTimestamp + toleranceMs) return null;

        var ticks = DrainTicksFor(unitId)
            .Count(tick => tick > pre.Timestamp && tick <= post.Timestamp);

        var cleanses = _cleanseCasts
            .Count(cast => cast.Timestamp >= pre.Timestamp && cast.Timestamp <= post.Timestamp);

        return new StaggerCleanse(
            unitId,
            startTimestamp,
            pre.Timestamp,
            pre.Amount,
            post.Timestamp,
            post.Amount,
            pre.Amount - post.Amount,
            ticks,
            cleanses);
    }

    /// <summary>
    /// The maximum hit points recorded for <paramref name="unitId"/> nearest to
    /// <paramref name="timestamp"/>, preferring the closest entry before it and falling back to the
    /// closest after, or <see langword="null"/> when no entry has one.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to read around.</param>
    public long? MaxHitPointsOf(int unitId, int timestamp)
    {
        if (!_snapshots.TryGetValue(unitId, out var snapshots)) return null;

        var index = FirstIndexAtOrAfter(snapshots, timestamp);

        for (var i = index - 1; i >= 0; i--)
        {
            if (snapshots[i].MaxHitPoints > 0) return snapshots[i].MaxHitPoints;
        }

        for (var i = index; i < snapshots.Count; i++)
        {
            if (snapshots[i].MaxHitPoints > 0) return snapshots[i].MaxHitPoints;
        }

        return null;
    }

    /// <summary>
    /// The Amend Fate and Restore Continuity casts the player made between <paramref name="startTimestamp"/>
    /// and <paramref name="endTimestamp"/>, both bounds inclusive.
    /// </summary>
    /// <param name="startTimestamp">The first instant to include.</param>
    /// <param name="endTimestamp">The last instant to include.</param>
    public IReadOnlyList<CleanseCast> CleanseCastsBetween(int startTimestamp, int endTimestamp) =>
        [.. _cleanseCasts.Where(cast => cast.Timestamp >= startTimestamp && cast.Timestamp <= endTimestamp)];

    /// <summary>
    /// The Stagger the party accumulated between <paramref name="startTimestamp"/> and
    /// <paramref name="endTimestamp"/>, in hit points: every rise in a unit's Stagger pool inside the
    /// window, summed across every unit with one.
    /// </summary>
    /// <param name="startTimestamp">The first instant to include.</param>
    /// <param name="endTimestamp">The last instant to include.</param>
    public int StaggerGeneratedBetween(int startTimestamp, int endTimestamp)
    {
        var generated = 0;

        foreach (var snapshots in _snapshots.Values)
        {
            for (var i = 1; i < snapshots.Count; i++)
            {
                if (snapshots[i - 1].Timestamp < startTimestamp) continue;
                if (snapshots[i].Timestamp > endTimestamp) break;

                var rise = snapshots[i].Amount - snapshots[i - 1].Amount;
                if (rise > 0) generated += rise;
            }
        }

        return generated;
    }

    /// <summary>
    /// The mana Amend Fate and Restore Continuity generated between <paramref name="startTimestamp"/>
    /// and <paramref name="endTimestamp"/>.
    /// </summary>
    /// <param name="startTimestamp">The first instant to include.</param>
    /// <param name="endTimestamp">The last instant to include.</param>
    public int ManaFromCleansingBetween(int startTimestamp, int endTimestamp) =>
        ChronaTracker.GeneratedByAbilityBetween(
            ResourceTypes.Mana, Spells.AmendFate.FSLID, startTimestamp, endTimestamp)
        + ChronaTracker.GeneratedByAbilityBetween(
            ResourceTypes.Mana, Spells.RestoreContinuity.FSLID, startTimestamp, endTimestamp);

    /// <summary>
    /// Reconstructs the Stagger a cleanse cast at <paramref name="castTimestamp"/> cleared off
    /// <paramref name="unitId"/>, by bracketing the cast with the unit's last Stagger before it and its
    /// first from it onwards. Returns <see langword="null"/> when either end of the bracket is missing or
    /// the one after the cast falls more than <paramref name="windowMs"/> later.
    /// </summary>
    /// <param name="unitId">The unit the cleanse was aimed at.</param>
    /// <param name="castTimestamp">When the cleanse cast completed.</param>
    /// <param name="windowMs">How long after the cast the closing figure may fall and still close the bracket.</param>
    public StaggerCleanse? MeasureCleanse(int unitId, int castTimestamp, int windowMs)
    {
        if (LatestBefore(unitId, castTimestamp) is not { } pre) return null;
        if (EarliestAfter(unitId, castTimestamp) is not { } post) return null;
        if (post.Timestamp > castTimestamp + windowMs) return null;

        var ticks = DrainTicksFor(unitId)
            .Count(tick => tick > pre.Timestamp && tick <= post.Timestamp);

        var cleanses = _cleanseCasts
            .Count(cast => cast.Timestamp >= pre.Timestamp
                && cast.Timestamp <= post.Timestamp
                && cast.Timestamp != castTimestamp);

        return new StaggerCleanse(
            unitId,
            castTimestamp,
            pre.Timestamp,
            pre.Amount,
            post.Timestamp,
            post.Amount,
            pre.Amount - post.Amount,
            ticks,
            cleanses);
    }

    [On<Event>]
    private void OnEvent(Event e)
    {
        if (e is IHasSourceEvent source)
            Record(source.SourceId, e.SourceResources, e.Timestamp);

        if (e is IHasTargetEvent target)
            Record(target.TargetId, e.TargetResources, e.Timestamp);
    }

    [On<DeathEvent>]
    private void OnDeath(DeathEvent e)
    {
        if (!_deaths.TryGetValue(e.TargetId, out var deaths))
            _deaths[e.TargetId] = deaths = [];

        deaths.Add(e.Timestamp);
    }

    [On<DamageEvent>]
    private void OnDamage(DamageEvent e)
    {
        if (e.Ability?.Id != StaggerDrain) return;

        if (!_drainTicks.TryGetValue(e.TargetId, out var ticks))
            _drainTicks[e.TargetId] = ticks = [];

        ticks.Add(e.Timestamp);
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseCast(CastEvent e)
    {
        var cast = new CleanseCast(e.Timestamp, e.Ability.Id, e.TargetId);
        _cleanseCasts.Add(cast);
        _latestCleanseCastByAbility[e.Ability.Id] = cast;
    }

    [On<HealEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseHeal(HealEvent e)
    {
        if (_latestCleanseCastByAbility.TryGetValue(e.Ability.Id, out var cast))
            cast.AddHealTarget(e.TargetId);
    }

    private (int Total, int Casts) CleanseTotals()
    {
        var total = 0;
        var casts = 0;

        foreach (var cast in _cleanseCasts)
        {
            var cleansed = 0;
            var measured = false;

            foreach (var unitId in cast.HealTargets)
            {
                if (MeasureCleanse(unitId, cast.Timestamp, CleanseBracketWindowMs)
                    is { HasInterveningEvent: false, ClearedAmount: > 0 } cleanse)
                {
                    cleansed += cleanse.ClearedAmount;
                    measured = true;
                }
            }

            if (!measured) continue;

            total += cleansed;
            casts++;
        }

        return (total, casts);
    }

    private void Record(int unitId, ActorResources? resources, int timestamp)
    {
        if (resources is null) return;

        ClassResource? stagger = null;
        foreach (var resource in resources.Resources)
        {
            if (resource.Type != ResourceTypes.Stagger) continue;
            stagger = resource;
            break;
        }

        if (stagger is null) return;

        if (!_snapshots.TryGetValue(unitId, out var snapshots))
        {
            _snapshots[unitId] = snapshots = [];
            _trackedUnitIds.Add(unitId);
        }

        if (snapshots.Count > 0)
        {
            var last = snapshots[^1];
            if (last.Amount == stagger.Amount
                && last.Max == stagger.Max
                && last.HitPoints == resources.HitPoints
                && last.MaxHitPoints == resources.MaxHitPoints)
                return;
        }

        snapshots.Add(new StaggerSnapshot(
            timestamp,
            stagger.Amount,
            stagger.Max,
            resources.HitPoints,
            resources.MaxHitPoints));
    }

    private static int FirstIndexAtOrAfter(List<StaggerSnapshot> snapshots, int timestamp)
    {
        var low = 0;
        var high = snapshots.Count;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (snapshots[mid].Timestamp < timestamp)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }
}

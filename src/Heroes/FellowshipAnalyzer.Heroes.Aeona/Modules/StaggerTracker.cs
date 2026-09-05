using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;
using SpellKind = FellowshipAnalyzer.Core.Common.Spells.SpellKind;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// One reading of a unit's Stagger pool, taken from the <see cref="ActorResources"/> block a log event
/// carries for that unit.
/// </summary>
/// <param name="Timestamp">When the reading was taken.</param>
/// <param name="Amount">The Stagger pending on the unit, in hit points. <see cref="Analysis.Normalizers.ResourceNormalizer"/> has already divided the raw log value by 100.</param>
/// <param name="Max">The pool's cap as the log reports it. Fellowship sends the no-maximum sentinel for Stagger, which normalization leaves as <c>-1</c>, so the pool is uncapped and <paramref name="Amount"/> is an absolute figure rather than a percentage.</param>
/// <param name="HitPoints">The unit's current hit points at the same instant.</param>
/// <param name="MaxHitPoints">The unit's maximum hit points at the same instant.</param>
public sealed record StaggerSnapshot(int Timestamp, int Amount, int Max, long HitPoints, long MaxHitPoints);

/// <summary>
/// One Amend Fate or Restore Continuity cast by the player, with the targets its heals landed on.
/// </summary>
/// <param name="timestamp">When the cast completed.</param>
/// <param name="ability">The FSLID of the ability cast, either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
/// <param name="targetId">The target id the cast event carried. Fellowship logs both cleanses with no named target, so this reads <c>-1</c> on real casts; read <see cref="HealTargets"/> instead.</param>
public sealed class CleanseCast(int timestamp, FSLID ability, int targetId)
{
    private readonly List<int> _healTargets = [];

    /// <summary>When the cast completed.</summary>
    public int Timestamp { get; } = timestamp;

    /// <summary>The ability cast, either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</summary>
    public FSLID Ability { get; } = ability;

    /// <summary>
    /// The target id the cast event carried, which is <c>-1</c> for both cleanses because Fellowship
    /// names no target on the cast itself.
    /// </summary>
    public int TargetId { get; } = targetId;

    /// <summary>
    /// The units this cast's heals landed on, in the order they were healed, attributed by taking every
    /// heal of the same ability up to the player's next cast of it. Amend Fate lands on one ally;
    /// Restore Continuity lands on the whole party including the player.
    /// </summary>
    public IReadOnlyList<int> HealTargets => _healTargets;

    internal void AddHealTarget(int unitId)
    {
        if (!_healTargets.Contains(unitId))
            _healTargets.Add(unitId);
    }
}

/// <summary>
/// The Stagger a cleanse cast cleared off one unit, reconstructed from the unit's Stagger snapshots
/// either side of the cast because Fellowship emits no cleanse event of its own.
/// </summary>
/// <param name="UnitId">The unit whose pool was measured.</param>
/// <param name="CastTimestamp">The cast the measurement brackets.</param>
/// <param name="PreTimestamp">When the pre-cast reading was taken. The further this sits ahead of <paramref name="CastTimestamp"/>, the staler the reading.</param>
/// <param name="PreAmount">The unit's Stagger at <paramref name="PreTimestamp"/>, in hit points.</param>
/// <param name="PostTimestamp">When the post-cast reading was taken.</param>
/// <param name="PostAmount">The unit's Stagger at <paramref name="PostTimestamp"/>, in hit points.</param>
/// <param name="ClearedAmount">The pool's fall across the two readings, in hit points. Negative when the pool grew, which happens when the unit took staggered damage inside the bracket.</param>
/// <param name="InterveningTickCount">Stagger drain ticks observed on the unit after <paramref name="PreTimestamp"/> and up to <paramref name="PostTimestamp"/>, each of which drained the pool independently of the cleanse.</param>
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
    /// A false reading is not proof the bracket was clean: Fellowship Logs streams only events the
    /// analyzed player is source or target of, so another unit's drain ticks never reach the parser.
    /// </summary>
    public bool HasInterveningEvent => InterveningTickCount > 0 || InterveningCleanseCount > 0;
}

/// <summary>
/// Reconstructs every party member's Stagger pool across the dungeon.
/// <para>
/// Stagger is <see cref="ResourceTypes.Stagger"/> inside the <see cref="Event.SourceResources"/> and
/// <see cref="Event.TargetResources"/> blocks events carry, and no Core module aggregates resources for a
/// unit other than the analyzed player, so this module harvests those blocks from an unfiltered
/// <c>[On&lt;Event&gt;]</c> handler the way <c>ResourceTracker</c> does. A unit carrying a Stagger entry is a
/// party member by construction, so no actor table is needed to decide what to keep.
/// </para>
/// <para>
/// Cleansing produces no event of its own: the Amend Fate and Restore Continuity stagger-removal effects
/// emit nothing and there is no resource-change event on the target, so the amount cleared is only
/// visible as a fall in the target's Stagger between consecutive snapshots. <see cref="MeasureCleanse"/>
/// reconstructs it and reports what else moved the pool inside the bracket.
/// </para>
/// </summary>
public sealed partial class StaggerTracker : Analyzer
{
    /// <summary>
    /// The effect the Stagger pool drains through, as periodic self-damage on the staggered unit roughly
    /// every three seconds. Codex <c>effect 2696</c>, named "Stagger"; the codex record carries no
    /// description, and the effect has no spell-registry member because it belongs to no hero's kit.
    /// </summary>
    private const int StaggerDrainEffectId = 2696;

    /// <summary>
    /// How old a Stagger reading may be and still describe the pool at the instant asked about. A reading
    /// further back than this supports no judgement about what the pool held, so the caller is given
    /// nothing rather than a stale figure.
    /// </summary>
    public const int ReadingMaxAgeMs = 1000;

    /// <summary>How long after a cleanse cast a reading may fall and still close its bracket.</summary>
    public const int CleanseBracketWindowMs = 500;

    private static readonly FSLID StaggerDrain = FSLID.FromNative(SpellKind.Effect, StaggerDrainEffectId);

    private readonly Dictionary<int, List<StaggerSnapshot>> _snapshots = [];
    private readonly List<int> _trackedUnitIds = [];
    private readonly Dictionary<int, List<int>> _drainTicks = [];
    private readonly List<CleanseCast> _cleanseCasts = [];
    private readonly Dictionary<int, CleanseCast> _latestCleanseCastByAbility = [];
    private readonly Dictionary<int, List<int>> _deaths = [];

    /// <summary>
    /// Every unit a Stagger pool was observed on, in the order each was first seen. Fellowship gives a
    /// Stagger pool to every party member, so this is the party as far as the event stream reveals it.
    /// </summary>
    public IReadOnlyList<int> TrackedUnitIds => _trackedUnitIds;

    /// <summary>
    /// The party's tank actor ids, resolved from the report's actor list by parsing each actor's hero
    /// and keeping those whose <see cref="HeroRole"/> is <see cref="HeroRole.Tank"/>. Empty when the
    /// report carries no actor list.
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
    /// The Stagger readings taken for <paramref name="unitId"/>, in chronological order with consecutive
    /// identical readings collapsed to the first of the run. Empty for a unit that carries no Stagger pool.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    public IReadOnlyList<StaggerSnapshot> SnapshotsFor(int unitId) =>
        _snapshots.TryGetValue(unitId, out var snapshots) ? snapshots : [];

    /// <summary>
    /// The timestamps of the Stagger drain ticks observed on <paramref name="unitId"/>, in chronological
    /// order. Fellowship Logs streams only events the analyzed player is source or target of, so drain
    /// ticks are observable on the player and on nobody else; an empty list is not evidence the unit's
    /// pool never drained.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    public IReadOnlyList<int> DrainTicksFor(int unitId) =>
        _drainTicks.TryGetValue(unitId, out var ticks) ? ticks : [];

    /// <summary>
    /// The last Stagger reading taken for <paramref name="unitId"/> strictly before
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
    /// The first Stagger reading taken for <paramref name="unitId"/> at or after
    /// <paramref name="timestamp"/>, or <see langword="null"/> when the unit has none that late. The
    /// boundary is inclusive so that a reading taken in the same millisecond as a cast counts as the
    /// reading after it, which is where Fellowship puts a cleanse's own heal.
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
    /// as a fraction of the unit's maximum hit points, or <see langword="null"/> when no reading is
    /// available. Stagger's reported maximum is Fellowship's no-maximum sentinel, so the pool is uncapped
    /// and the amount is an absolute hit-point figure; maximum hit points is the only meaningful scale, and
    /// the fraction exceeds 1 when a unit holds more Stagger than its health bar. A value of 0.4 is the 40%
    /// threshold at which cleansing takes priority over Oblivion.
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
    /// The same fraction as <see cref="StaggerFractionOfMaxHp(int, int)"/>, given only when the reading
    /// behind it was taken within <paramref name="maxAgeMs"/> of <paramref name="timestamp"/>.
    /// </summary>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to read back from.</param>
    /// <param name="maxAgeMs">How far back the reading may have been taken.</param>
    public double? StaggerFractionOfMaxHp(int unitId, int timestamp, int maxAgeMs)
    {
        if (LatestBefore(unitId, timestamp) is not { } snapshot) return null;
        if (timestamp - snapshot.Timestamp > maxAgeMs) return null;
        if (MaxHitPointsOf(unitId, timestamp) is not { } maxHitPoints) return null;

        return (double)snapshot.Amount / maxHitPoints;
    }

    /// <summary>
    /// Whether <paramref name="unitId"/> was alive at <paramref name="timestamp"/>: true until the unit
    /// dies, and false from that death until the unit's next reading showing hit points above zero.
    /// </summary>
    /// <remarks>
    /// Core's <c>DeathTracker</c> records the analyzed player alone, so party deaths are reconstructed
    /// here from the unfiltered death stream and the hit points every Stagger reading carries.
    /// </remarks>
    /// <param name="unitId">The unit to read.</param>
    /// <param name="timestamp">The instant to judge.</param>
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
    /// The Stagger a single clean cast of <paramref name="abilityId"/> removes, taken from the report
    /// itself as the median of every cast whose bracket nothing else disturbed and whose target still
    /// held Stagger afterwards, so the cast cleared its whole amount rather than emptying the pool.
    /// <see langword="null"/> when the report holds no such cast.
    /// </summary>
    /// <remarks>
    /// The median rather than an extreme: brackets in real reports catch drain ticks and incoming
    /// staggered damage, which pushes individual measurements well above and below the amount the ability
    /// actually removes.
    /// </remarks>
    /// <param name="abilityId">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
    public int? SingleCastCleanseAmount(FSLID abilityId)
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
    /// single cast. The bracket opens on the unit's last reading before the window and closes on its first
    /// reading from the window's end onwards, within <paramref name="toleranceMs"/> of it.
    /// </summary>
    /// <param name="unitId">The unit to measure.</param>
    /// <param name="startTimestamp">When the window opened.</param>
    /// <param name="endTimestamp">When the window closed.</param>
    /// <param name="toleranceMs">How long after the window a reading may fall and still close the bracket.</param>
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
    /// <paramref name="timestamp"/>, preferring the closest reading before it and falling back to the
    /// closest after, or <see langword="null"/> when no reading carries one.
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
    /// Reconstructs the Stagger a cleanse cast at <paramref name="castTimestamp"/> cleared off
    /// <paramref name="unitId"/>, by bracketing the cast with the unit's last reading before it and its
    /// first reading from it onwards. Returns <see langword="null"/> when either reading is missing or the
    /// reading after the cast falls more than <paramref name="windowMs"/> later, rather than reporting a
    /// delta the readings do not support.
    /// </summary>
    /// <param name="unitId">The unit the cleanse was aimed at.</param>
    /// <param name="castTimestamp">When the cleanse cast completed.</param>
    /// <param name="windowMs">How long after the cast a reading may fall and still close the bracket.</param>
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

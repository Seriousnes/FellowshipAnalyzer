using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Core module answering which units a pull contains, how many of them are alive at an instant, and
/// which of them an effect is active on.
/// <para>
/// The module declares no <c>[On&lt;TEvent&gt;]</c> handlers. Its state is built once, lazily, on the
/// first query, by a single pass over <see cref="CombatLogParser.Events"/>, and retained. Every
/// accessor filters that state by timestamp rather than accumulating up to a dispatch cursor, so
/// <see cref="AliveAt(PullStartEvent, int)"/> returns the same answer during dispatch and from a guide
/// rendered after the parse.
/// </para>
/// <para>
/// A pull's roster is a projection, not an expansion: <see cref="Analysis.Combatants"/> seeds every
/// spawn the report's rosters name, and this module selects the ones a pull admits - those its own
/// enemy NPC list names by instance span, or the whole seeded population for a dungeon exposing no
/// dungeon pulls - less the pets. Seeding covers every roster in the report, so a projection can
/// never want a key the population lacks. A roster key is not always the key an event writes: an
/// actor with one spawn omits the instance on its events, so its auras accrue under a
/// <c>null</c>-instance key while its roster entry names instance 1. <see cref="Resolve"/> bridges
/// the two; do not read a <see cref="Roster"/> key straight out of
/// <see cref="Combatants.AuraInstanceCount"/>.
/// </para>
/// <para>
/// A roster is dungeon metadata and needs no master data. Hostility does: a death event and an aura
/// event name an actor and nothing more, so telling an enemy from the selected player, an ally, or the
/// Environment actor is read from <see cref="CombatLogParser.Actors"/>. An analysis run given no actors
/// therefore keeps its rosters and admits nothing else: no unrostered unit, no death, and no aura.
/// Supply <see cref="CombatLogParser.Actors"/> in any fixture reading those.
/// </para>
/// </summary>
[Dependency<Combatants>]
public sealed partial class Enemies : Module
{
    private const string NpcActorType = "NPC";

    private State Index => field ??= Build();

    private static HashSet<int> PetActorIds(IReadOnlyList<DungeonNpc>? npcs)
    {
        HashSet<int> petActorIds = [];
        foreach (var npc in npcs ?? [])
        {
            if (npc.PetOwner is not null) petActorIds.Add(npc.Id);
        }
        return petActorIds;
    }

    /// <summary>
    /// Whether <paramref name="actorId"/> is an enemy: an actor the report master data types as an NPC,
    /// with a non-negative id, that <see cref="DungeonNpc.PetOwner"/> does not name as a pet. The
    /// Environment actor is id <c>-1</c>, a damage source and cast target rather than a unit, so it is
    /// excluded. Hostility is a property of the actor, so <paramref name="instance"/> is accepted for
    /// callers passing an event's actor and instance together and does not narrow the answer.
    /// </summary>
    public bool IsEnemy(int actorId, int? instance = null) => Index.EnemyActorIds.Contains(actorId);

    /// <summary>
    /// The <see cref="UnitKey"/> an event's actor and instance name within <paramref name="pull"/>, or
    /// <c>null</c> when the actor is not an enemy. An actor with exactly one instance in the dungeon omits
    /// the instance on its events, so a <c>null</c> <paramref name="instance"/> resolves to that actor's
    /// sole roster instance in the pull, and keeps its <c>null</c> instance where no roster names the
    /// actor once.
    /// </summary>
    public UnitKey? Resolve(int actorId, int? instance, PullStartEvent pull)
    {
        if (!IsEnemy(actorId)) return null;
        if (instance is not null) return new UnitKey(actorId, instance);

        return Index.ByPull.TryGetValue(pull.Index, out var population)
            ? population.SoleRosterInstance(actorId)
            : new UnitKey(actorId, null);
    }

    /// <summary>
    /// The units <paramref name="pull"/> names, one per instance, from the pull's own enemy NPC list, or
    /// the whole seeded population for a dungeon exposing no dungeon pulls. This is the enemy count a
    /// pull is measured against. Two pulls of one dungeon can name the same unit, so a death is
    /// recorded against that unit in every pull naming it and it never comes back alive.
    /// </summary>
    public IReadOnlyList<EnemyUnit> Roster(PullStartEvent pull) =>
        Index.ByPull.TryGetValue(pull.Index, out var population) ? population.Roster : [];

    /// <summary>
    /// Every unit alive at some point inside <paramref name="pull"/>: its roster, plus each enemy the
    /// events name that no roster does.
    /// </summary>
    public IReadOnlyList<EnemyUnit> Population(PullStartEvent pull) =>
        Index.ByPull.TryGetValue(pull.Index, out var population) ? population.Units : [];

    /// <summary>
    /// How many of <paramref name="pull"/>'s enemies are alive at <paramref name="timestamp"/>. A unit
    /// is alive from the moment it enters, which is <see cref="PullStartEvent.StartTime"/> for a roster
    /// unit and the first event naming it for any other, up to but not including its death. A unit still
    /// alive at <see cref="PullStartEvent.EndTime"/> despawns and is counted there. Returns 0 outside the
    /// pull window, and exceeds <see cref="Roster"/>'s count while an unrostered unit is alive.
    /// </summary>
    public int AliveAt(PullStartEvent pull, int timestamp)
    {
        if (timestamp < pull.StartTime || timestamp > pull.EndTime) return 0;
        if (!Index.ByPull.TryGetValue(pull.Index, out var population)) return 0;

        var alive = 0;
        foreach (var unit in population.Units)
        {
            if (unit.IsAliveAt(timestamp)) alive++;
        }
        return alive;
    }

    /// <summary>
    /// How many enemies are alive at <paramref name="timestamp"/> in whichever pull covers it, or 0
    /// outside every pull window. Every pull is known from the moment the state is built, so this reads
    /// the same during dispatch and after the parse. One pull's end can be the next one's start, and
    /// the later pull answers there, matching the close-before-open <see cref="CombatLogParser.BeginPull"/>
    /// performs at that instant.
    /// </summary>
    public int AliveAt(int timestamp) =>
        PullAt(Index.Pulls, timestamp) is { } pull ? AliveAt(pull, timestamp) : 0;

    /// <summary>
    /// How many of <paramref name="pull"/>'s enemies were alive across
    /// <paramref name="from"/>..<paramref name="to"/>, as contiguous bands clipped to the pull window.
    /// A band runs from its <see cref="AliveBand.Start"/> to where the next one begins; the last band
    /// ends at <paramref name="to"/> and includes it.
    /// </summary>
    public IReadOnlyList<AliveBand> AliveBetween(PullStartEvent pull, int from, int to)
    {
        var start = Math.Max(from, pull.StartTime);
        var end = Math.Min(to, pull.EndTime);
        if (end < start) return [];

        List<int> changes = [];
        foreach (var unit in Population(pull))
        {
            changes.Add(unit.Entered);
            if (unit.Died is { } died) changes.Add(died);
        }

        return [.. Bands(changes, start, end, timestamp => AliveAt(pull, timestamp))
            .Select(static band => new AliveBand(band.Start, band.End, band.Value))];
    }

    /// <summary>The most enemies alive at once anywhere in <paramref name="pull"/>.</summary>
    public int PeakAlive(PullStartEvent pull)
    {
        var peak = 0;
        foreach (var band in AliveBetween(pull, pull.StartTime, pull.EndTime))
            peak = Math.Max(peak, band.Alive);
        return peak;
    }

    /// <summary>
    /// Every enemy death between <paramref name="from"/> and <paramref name="to"/> inclusive, in the
    /// order they happened. The merged stream carries player deaths too, and they are absent here.
    /// </summary>
    public IReadOnlyList<EnemyDeath> DeathsBetween(int from, int to) =>
        [.. Index.Deaths.Where(death => death.Timestamp >= from && death.Timestamp <= to)];

    /// <summary>
    /// Whether the effect is active on at least one enemy at <paramref name="timestamp"/>, optionally
    /// restricted to auras applied by <paramref name="sourceId"/>.
    /// </summary>
    public bool AnyHasAura(SpellRef effect, long timestamp, int? sourceId = null)
    {
        foreach (var (key, entity) in Combatants.Units)
        {
            if (IsEnemy(key.ActorId) && entity.GetAuraInstanceCount(effect, timestamp, sourceId) > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// How many enemies the effect is active on at <paramref name="timestamp"/>, optionally restricted
    /// to auras applied by <paramref name="sourceId"/>.
    /// </summary>
    public int CountWithAura(SpellRef effect, long timestamp, int? sourceId = null)
        => WithAura(effect, timestamp, sourceId).Count;

    /// <summary>
    /// The keys of every enemy the effect is active on at <paramref name="timestamp"/>, optionally
    /// restricted to auras applied by <paramref name="sourceId"/>. Keys come back as the events wrote
    /// them, so they read straight back into <see cref="Combatants.AuraInstanceCount"/> and
    /// <see cref="Combatants.AuraStackSum"/>.
    /// </summary>
    public IReadOnlyCollection<UnitKey> WithAura(SpellRef effect, long timestamp, int? sourceId = null)
    {
        var keys = new List<UnitKey>();
        foreach (var (key, entity) in Combatants.Units)
        {
            if (IsEnemy(key.ActorId) && entity.GetAuraInstanceCount(effect, timestamp, sourceId) > 0)
                keys.Add(key);
        }
        return keys;
    }

    /// <summary>
    /// Every window of an effect on an enemy that overlaps <paramref name="from"/>..<paramref name="to"/>,
    /// one per application, each clipped to that range and the whole ordered by start. Windows overlap,
    /// both across enemies and across concurrent applications on one enemy.
    /// </summary>
    public IReadOnlyList<AuraWindow> AuraWindows(SpellRef effect, int from, int to, int? sourceId = null)
    {
        var windows = new List<AuraWindow>();
        foreach (var (key, entity) in Combatants.Units)
        {
            if (!IsEnemy(key.ActorId)) continue;
            windows.AddRange(entity.GetAuraWindows(effect, from, to, sourceId));
        }
        windows.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        return windows;
    }

    /// <summary>
    /// The union of <see cref="AuraWindows"/>, which is when the effect was active on at least one
    /// enemy.
    /// </summary>
    public IReadOnlyList<AuraWindow> MergedAuraWindows(SpellRef effect, int from, int to, int? sourceId = null)
        => Merge(AuraWindows(effect, from, to, sourceId));

    /// <summary>
    /// One timeline covering every effect in <paramref name="effects"/>, for an ability that applies
    /// more than one effect to the same unit.
    /// </summary>
    public IReadOnlyList<AuraWindow> MergedAuraWindows(IReadOnlyList<SpellRef> effects, int from, int to, int? sourceId = null)
    {
        var windows = new List<AuraWindow>();
        foreach (var effect in effects)
            windows.AddRange(AuraWindows(effect, from, to, sourceId));
        return Merge(windows);
    }

    /// <summary>
    /// How many enemies the effect was active on at once across
    /// <paramref name="from"/>..<paramref name="to"/>, as contiguous bands. An enemy with concurrent
    /// applications counts once. A band runs from its <see cref="AuraTargetBand.Start"/> to where the
    /// next one begins; the last band ends at <paramref name="to"/> and includes it.
    /// </summary>
    public IReadOnlyList<AuraTargetBand> AuraTargetsBetween(SpellRef effect, int from, int to, int? sourceId = null)
    {
        if (to < from) return [];

        List<AuraWindow> perEnemy = [];
        foreach (var (key, entity) in Combatants.Units)
        {
            if (!IsEnemy(key.ActorId)) continue;
            perEnemy.AddRange(Merge([.. entity.GetAuraWindows(effect, from, to, sourceId)]));
        }

        List<int> changes = [];
        foreach (var window in perEnemy)
        {
            changes.Add(window.Start);
            changes.Add(window.End + 1);
        }

        return [.. Bands(changes, from, to, timestamp => CountActive(perEnemy, timestamp))
            .Select(static band => new AuraTargetBand(band.Start, band.End, band.Value))];
    }

    /// <summary>
    /// The most enemies the effect was active on at once across
    /// <paramref name="from"/>..<paramref name="to"/>.
    /// </summary>
    public int PeakAuraTargets(SpellRef effect, int from, int to, int? sourceId = null)
    {
        var peak = 0;
        foreach (var band in AuraTargetsBetween(effect, from, to, sourceId))
            peak = Math.Max(peak, band.Targets);
        return peak;
    }

    private static int CountActive(List<AuraWindow> windows, int timestamp)
    {
        var active = 0;
        foreach (var window in windows)
        {
            if (window.Start <= timestamp && timestamp <= window.End) active++;
        }
        return active;
    }

    private static IReadOnlyList<AuraWindow> Merge(IReadOnlyList<AuraWindow> windows)
    {
        if (windows.Count == 0) return [];

        var ordered = windows.OrderBy(static window => window.Start).ToArray();
        List<AuraWindow> merged = [];
        var start = ordered[0].Start;
        var end = ordered[0].End;

        foreach (var window in ordered.AsSpan(1))
        {
            if (window.Start > end)
            {
                merged.Add(new AuraWindow(start, end));
                (start, end) = (window.Start, window.End);
                continue;
            }

            end = Math.Max(end, window.End);
        }

        merged.Add(new AuraWindow(start, end));
        return merged;
    }

    private static List<(int Start, int End, int Value)> Bands(List<int> changes, int from, int to, Func<int, int> valueAt)
    {
        List<int> boundaries = [from];
        changes.Sort();
        foreach (var change in changes)
        {
            if (change <= from || change > to) continue;
            if (change != boundaries[^1]) boundaries.Add(change);
        }

        List<(int Start, int End, int Value)> bands = [];
        for (var i = 0; i < boundaries.Count; i++)
        {
            var start = boundaries[i];
            var end = i + 1 < boundaries.Count ? boundaries[i + 1] : to;
            var value = valueAt(start);

            if (bands.Count > 0 && bands[^1].Value == value)
                bands[^1] = (bands[^1].Start, end, value);
            else
                bands.Add((start, end, value));
        }

        return bands;
    }

    private static PullStartEvent? PullAt(List<PullStartEvent> pulls, int timestamp)
    {
        for (var i = pulls.Count - 1; i >= 0; i--)
        {
            if (timestamp >= pulls[i].StartTime && timestamp <= pulls[i].EndTime) return pulls[i];
        }
        return null;
    }

    private State Build()
    {
        var petActorIds = PetActorIds(Owner.Dungeon.EnemyNpcs);

        HashSet<int> enemyActorIds = [];
        foreach (var actor in Owner.Actors)
        {
            if (actor.Id < 0) continue;
            if (!string.Equals(actor.Type, NpcActorType, StringComparison.OrdinalIgnoreCase)) continue;
            if (petActorIds.Contains(actor.Id)) continue;
            enemyActorIds.Add(actor.Id);
        }

        List<PullStartEvent> pulls = [];
        foreach (var e in Owner.Events)
        {
            if (e is PullStartEvent start) pulls.Add(start);
        }
        pulls.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        var byPull = new Dictionary<int, PullPopulation>(pulls.Count);
        foreach (var pull in pulls)
        {
            var dungeonPull = Owner.Dungeon.DungeonPulls?
                .FirstOrDefault(candidate => candidate.Id == pull.Id);

            List<UnitKey> rosterKeys = [];
            foreach (var (key, unit) in Combatants.Units)
            {
                if (unit is not Enemy { Rostered: true }) continue;
                if (petActorIds.Contains(key.ActorId)) continue;
                if (dungeonPull is not null && !(dungeonPull.EnemyNpcs ?? []).Any(npc =>
                        npc.Id == key.ActorId
                        && key.Instance >= npc.LowInstance
                        && key.Instance <= npc.HighInstance))
                    continue;

                rosterKeys.Add(key);
            }
            rosterKeys.Sort(static (left, right) => left.ActorId != right.ActorId
                ? left.ActorId.CompareTo(right.ActorId)
                : Nullable.Compare(left.Instance, right.Instance));

            byPull[pull.Index] = new PullPopulation(pull, rosterKeys);
        }

        Dictionary<UnitKey, EnemyDeath> deaths = [];
        foreach (var e in Owner.Events)
        {
            if (e is PullStartEvent or PullEndEvent) continue;
            if (PullAt(pulls, e.Timestamp) is not { } pull) continue;

            var population = byPull[pull.Index];

            if (e is IHasSourceEvent source && enemyActorIds.Contains(source.SourceId))
            {
                var instance = (e as IHasSourceWithInstanceEvent)?.SourceInstance;
                population.Enter(population.Key(source.SourceId, instance), e.Timestamp);
            }

            if (e is not IHasTargetEvent target || !enemyActorIds.Contains(target.TargetId)) continue;

            var key = population.Key(target.TargetId, (e as IHasTargetWithInstanceEvent)?.TargetInstance);
            population.Enter(key, e.Timestamp);

            if (e is not DeathEvent death) continue;

            foreach (var candidate in byPull.Values)
                candidate.Die(key, death.Timestamp);

            if (!deaths.TryGetValue(key, out var recorded) || death.Timestamp < recorded.Timestamp)
                deaths[key] = new EnemyDeath(key, death.Timestamp, death.SourceId, death.Ability);
        }

        return new State(
            enemyActorIds,
            pulls,
            byPull,
            [.. deaths.Values.OrderBy(static death => death.Timestamp)]);
    }

    private sealed class State(
        HashSet<int> enemyActorIds,
        List<PullStartEvent> pulls,
        Dictionary<int, PullPopulation> byPull,
        List<EnemyDeath> deaths)
    {
        public HashSet<int> EnemyActorIds { get; } = enemyActorIds;

        public List<PullStartEvent> Pulls { get; } = pulls;

        public Dictionary<int, PullPopulation> ByPull { get; } = byPull;

        public List<EnemyDeath> Deaths { get; } = deaths;
    }

    private sealed class PullPopulation
    {
        private readonly Dictionary<UnitKey, EnemyUnit> _units = [];
        private readonly List<UnitKey> _order = [];
        private readonly Dictionary<int, List<int?>> _rosterInstances = [];
        private readonly int _rosterCount;

        public PullPopulation(PullStartEvent pull, List<UnitKey> rosterKeys)
        {
            foreach (var key in rosterKeys)
            {
                if (!_units.TryAdd(key, new EnemyUnit(key, EnemyOrigin.Rostered, pull.StartTime, null))) continue;

                _order.Add(key);
                if (!_rosterInstances.TryGetValue(key.ActorId, out var instances))
                    _rosterInstances[key.ActorId] = instances = [];
                instances.Add(key.Instance);
            }
            _rosterCount = _order.Count;
        }

        public IReadOnlyList<EnemyUnit> Roster => field ??= [.. _order.Take(_rosterCount).Select(key => _units[key])];

        public IReadOnlyList<EnemyUnit> Units => field ??= [.. _order.Select(key => _units[key])];

        public UnitKey SoleRosterInstance(int actorId) =>
            _rosterInstances.TryGetValue(actorId, out var instances) && instances.Count == 1
                ? new UnitKey(actorId, instances[0])
                : new UnitKey(actorId, null);

        public UnitKey Key(int actorId, int? instance) =>
            instance is null ? SoleRosterInstance(actorId) : new UnitKey(actorId, instance);

        public void Enter(UnitKey key, int timestamp)
        {
            if (_units.TryGetValue(key, out var unit))
            {
                if (unit.Origin == EnemyOrigin.Unrostered && timestamp < unit.Entered)
                    _units[key] = unit with { Entered = timestamp };
                return;
            }

            _units[key] = new EnemyUnit(key, EnemyOrigin.Unrostered, timestamp, null);
            _order.Add(key);
        }

        public void Die(UnitKey key, int timestamp)
        {
            if (!_units.TryGetValue(key, out var unit)) return;
            if (unit.Died is { } died && died <= timestamp) return;

            _units[key] = unit with { Died = timestamp };
        }
    }
}

/// <summary>Whether a pull's own enemy NPC list names a unit.</summary>
public enum EnemyOrigin
{
    /// <summary>The pull's enemy NPC list names the unit, so it is alive from the pull's start.</summary>
    Rostered,

    /// <summary>No roster names the unit, so it is alive from the first event naming it.</summary>
    Unrostered,
}

/// <summary>
/// One enemy unit within a pull. <see cref="Died"/> is <c>null</c> when the dungeon records no death for
/// the unit, which for a roster unit means it despawned at the pull's end. A <see cref="Died"/> outside
/// the pull's window belongs to a unit more than one pull names.
/// </summary>
/// <param name="Key">The actor and instance identifying the unit.</param>
/// <param name="Origin">Whether the pull's roster names the unit.</param>
/// <param name="Entered">When the unit became alive.</param>
/// <param name="Died">When the unit died, or <c>null</c> when the dungeon records no death for it.</param>
public readonly record struct EnemyUnit(UnitKey Key, EnemyOrigin Origin, int Entered, int? Died)
{
    /// <summary>Whether the unit is alive at <paramref name="timestamp"/>. A death is an exit at its own instant.</summary>
    public bool IsAliveAt(int timestamp) =>
        Entered <= timestamp && (Died is not { } died || timestamp < died);
}

/// <summary>One enemy death.</summary>
/// <param name="Key">The actor and instance identifying the unit that died.</param>
/// <param name="Timestamp">When it died.</param>
/// <param name="SourceId">The actor credited with the killing blow.</param>
/// <param name="Ability">The killing blow's ability.</param>
public readonly record struct EnemyDeath(UnitKey Key, int Timestamp, int SourceId, Ability Ability);

/// <summary>
/// How many enemies were alive across one stretch of a pull. The band runs from <see cref="Start"/> to
/// where the next band begins.
/// </summary>
/// <param name="Start">When the band begins.</param>
/// <param name="End">Where the next band begins, or the end of the queried range for the last band.</param>
/// <param name="Alive">Enemies alive throughout the band.</param>
public readonly record struct AliveBand(int Start, int End, int Alive)
{
    /// <summary>How long the band runs, in milliseconds.</summary>
    public int Duration => End - Start;
}

/// <summary>
/// How many enemies one effect was active on across one stretch of a range. The band runs from
/// <see cref="Start"/> to where the next band begins.
/// </summary>
/// <param name="Start">When the band begins.</param>
/// <param name="End">Where the next band begins, or the end of the queried range for the last band.</param>
/// <param name="Targets">Enemies the effect was active on throughout the band.</param>
public readonly record struct AuraTargetBand(int Start, int End, int Targets)
{
    /// <summary>How long the band runs, in milliseconds.</summary>
    public int Duration => End - Start;
}

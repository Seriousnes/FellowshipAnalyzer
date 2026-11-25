using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Core module that tracks buff/debuff state for every unit in the dungeon, keyed by
/// <see cref="UnitKey"/> so distinct spawns of one actor id stay separate. The rosters the report
/// declares seed the set: the analyzed player as the <see cref="FullCombatant"/> the parse context
/// supplies, every other group member as a <see cref="Combatant"/>, and one <see cref="Enemy"/> per
/// spawn the dungeon's enemy NPC list or any dungeon pull's names. Seeding the union of both means no
/// pull can name a spawn the population lacks. A unit no roster names is fabricated as an
/// <see cref="Enemy"/> when an event first targets it, and has <see cref="Enemy.Rostered"/>
/// <c>false</c> to say so. Exposes per-unit aura queries that resolve any unit including the analyzed
/// player. <see cref="Enemies"/> owns the enemy-facing queries, which need report master data to tell
/// an enemy from an ally, and projects each pull's roster out of this population.
/// </summary>
public sealed partial class Combatants : Analyzer
{
    private readonly Dictionary<UnitKey, Combatant> _units = [];

    /// <summary>Every tracked unit keyed by <see cref="UnitKey"/>.</summary>
    public Dictionary<UnitKey, Combatant> Units => _units;

    /// <summary>
    /// Seeds the tracked unit set from every roster the report declares and fabricates a prepull buff
    /// for each aura already active on the analyzed player.
    /// </summary>
    public Combatants(ParseContext parseContext)
    {
        var selected = parseContext.SelectedCombatant;
        _units[new UnitKey(selected.Id, null)] = selected;

        foreach (var friendlyPlayer in parseContext.Dungeon.FriendlyPlayers ?? [])
        {
            var key = new UnitKey(friendlyPlayer, null);
            if (!_units.ContainsKey(key)) _units[key] = new Combatant(friendlyPlayer);
        }

        foreach (var npc in parseContext.Dungeon.EnemyNpcs ?? [])
            SeedSpawns(npc.Id, 1, Math.Max(npc.InstanceCount, 1));

        foreach (var dungeonPull in parseContext.DungeonPulls ?? [])
        {
            foreach (var npc in dungeonPull.EnemyNpcs ?? [])
            {
                if (npc.Id is int id) SeedSpawns(id, npc.LowInstance, npc.HighInstance);
            }
        }

        foreach (var aura in selected.Auras)
        {
            var prepullBuff = new TrackedBuffEvent
            {
                Timestamp = selected.Info.Timestamp,
                Ability = new Ability { FSLID = aura.Ability, Name = aura.Name, Icon = aura.Icon },
                SourceId = aura.Source,
                TargetId = selected.Id,
                Start = selected.Info.Timestamp,
                Stacks = aura.Stacks,
                IsDebuff = aura.Source != selected.Id,
            };
            prepullBuff.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
            {
                Stacks = aura.Stacks,
                Timestamp = selected.Info.Timestamp,
            });
            selected.ApplyBuff(prepullBuff);
        }
    }

    /// <summary>The tracked unit for an actor id and spawn instance, or null when none is tracked.</summary>
    public Combatant? GetCombatant(int actorId, int? instance) => _units.GetValueOrDefault(new UnitKey(actorId, instance));

    private void SeedSpawns(int actorId, int lowInstance, int highInstance)
    {
        for (var instance = lowInstance; instance <= highInstance; instance++)
        {
            var key = new UnitKey(actorId, instance);
            if (!_units.ContainsKey(key)) _units[key] = new Enemy(actorId, instance, rostered: true);
        }
    }

    /// <summary>
    /// The number of concurrently-open windows of the effect active on a unit at <paramref name="timestamp"/>,
    /// optionally restricted to auras applied by <paramref name="sourceId"/>. Returns 0 when the unit is
    /// not tracked.
    /// </summary>
    public int AuraInstanceCount(int actorId, int? instance, SpellRef effect, long timestamp, int? sourceId = null)
        => GetCombatant(actorId, instance)?.GetAuraInstanceCount(effect, timestamp, sourceId) ?? 0;

    /// <summary>
    /// The stacks summed across every concurrently-open window of the effect on a unit at
    /// <paramref name="timestamp"/>, optionally restricted to auras applied by <paramref name="sourceId"/>.
    /// Returns 0 when the unit is not tracked. Read it while the parse is dispatching, since a window
    /// reports its live stack count rather than its count at <paramref name="timestamp"/>.
    /// </summary>
    public int AuraStackSum(int actorId, int? instance, SpellRef effect, long timestamp, int? sourceId = null)
        => GetCombatant(actorId, instance)?.GetAuraStackSum(effect, timestamp, sourceId) ?? 0;

    [On<ApplyBuffEvent>]
    private void OnApplyBuff(ApplyBuffEvent e) => ApplyBuff(e, isDebuff: false);

    [On<ApplyDebuffEvent>]
    private void OnApplyDebuff(ApplyDebuffEvent e) => ApplyBuff(e, isDebuff: true);

    private void ApplyBuff(BuffEvent e, bool isDebuff)
    {
        var entity = GetOrCreateEntity(e.TargetId, e.TargetInstance);

        var buff = new TrackedBuffEvent
        {
            Timestamp = e.Timestamp,
            Ability = e.Ability,
            SourceId = e.SourceId,
            TargetId = e.TargetId,
            Start = e.Timestamp,
            Stacks = 1,
            IsDebuff = isDebuff,
        };
        buff.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
        {
            Stacks = 1,
            Timestamp = e.Timestamp,
        });

        entity.ApplyBuff(buff);
    }

    [On<RemoveBuffEvent>]
    private void OnRemoveBuff(RemoveBuffEvent e) => RemoveBuff(e, isDebuff: false);

    [On<RemoveDebuffEvent>]
    private void OnRemoveDebuff(RemoveDebuffEvent e) => RemoveBuff(e, isDebuff: true);

    private void RemoveBuff(BuffEvent e, bool isDebuff)
    {
        var entity = GetOrCreateEntity(e.TargetId, e.TargetInstance);
        var existing = GetExistingBuff(entity, e.Ability.Id, e.SourceId);

        if (existing is not null)
        {
            var oldStacks = existing.Stacks;
            existing.End = e.Timestamp;
            existing.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
            {
                Stacks = 0,
                Timestamp = e.Timestamp,
            });

            FabricateStackChange(existing, e, oldStacks, 0, isDebuff);
        }
        else
        {
            var synthetic = new TrackedBuffEvent
            {
                Timestamp = e.Timestamp,
                Ability = e.Ability,
                SourceId = e.SourceId,
                TargetId = e.TargetId,
                Start = e.Timestamp,
                End = e.Timestamp,
                Stacks = 0,
                IsDebuff = isDebuff,
            };
            synthetic.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement { Stacks = 0, Timestamp = e.Timestamp });
            entity.ApplyBuff(synthetic);

            FabricateStackChange(synthetic, e, 1, 0, isDebuff);
        }
    }

    [On<RefreshBuffEvent>]
    private void OnRefreshBuff(RefreshBuffEvent e)
    {
        var entity = GetOrCreateEntity(e.TargetId, e.TargetInstance);
        GetExistingBuff(entity, e.Ability.Id, e.SourceId)?.RefreshHistory.Add(e.Timestamp);
    }

    [On<RefreshDebuffEvent>]
    private void OnRefreshDebuff(RefreshDebuffEvent e)
    {
        var entity = GetOrCreateEntity(e.TargetId, e.TargetInstance);
        GetExistingBuff(entity, e.Ability.Id, e.SourceId)?.RefreshHistory.Add(e.Timestamp);
    }

    [On<ApplyBuffStackEvent>]
    private void OnApplyBuffStack(ApplyBuffStackEvent e) => UpdateStack(e, isDebuff: false);

    [On<RemoveBuffStackEvent>]
    private void OnRemoveBuffStack(RemoveBuffStackEvent e) => UpdateStack(e, isDebuff: false);

    [On<ApplyDebuffStackEvent>]
    private void OnApplyDebuffStack(ApplyDebuffStackEvent e) => UpdateStack(e, isDebuff: true);

    [On<RemoveDebuffStackEvent>]
    private void OnRemoveDebuffStack(RemoveDebuffStackEvent e) => UpdateStack(e, isDebuff: true);

    private void UpdateStack(BuffEvent e, bool isDebuff)
    {
        var entity = GetOrCreateEntity(e.TargetId, e.TargetInstance);
        var existing = GetExistingBuff(entity, e.Ability.Id, e.SourceId);
        if (existing is null) return;

        if (e is not IBuffStackEvent stackEvent) return;
        var oldStacks = Math.Max(existing.Stacks, 1);
        existing.Stacks = stackEvent.Stack;
        existing.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
        {
            Stacks = stackEvent.Stack,
            Timestamp = e.Timestamp,
        });

        FabricateStackChange(existing, e, oldStacks, stackEvent.Stack, isDebuff);
    }

    [On<DeathEvent>]
    private void OnDeath(DeathEvent e)
    {
        if (!_units.TryGetValue(new UnitKey(e.TargetId, e.TargetInstance), out var entity)) return;

        foreach (var buff in entity.Buffs.Where(b => b.End is null))
        {
            buff.End = e.Timestamp;
            buff.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
            {
                Stacks = 0,
                Timestamp = e.Timestamp,
            });
        }
    }

    [On<DungeonEndEvent>]
    private void OnDungeonEnd(DungeonEndEvent e)
    {
        foreach (var entity in _units.Values)
        {
            foreach (var buff in entity.Buffs.Where(b => b.End is null))
            {
                buff.End = e.Timestamp;
                buff.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
                {
                    Stacks = 0,
                    Timestamp = e.Timestamp,
                });
            }
        }
    }

    private Combatant GetOrCreateEntity(int targetId, int? instance)
    {
        var key = new UnitKey(targetId, instance);
        if (!_units.TryGetValue(key, out var entity))
        {
            entity = new Enemy(targetId, instance);
            _units[key] = entity;
        }
        return entity;
    }

    private static TrackedBuffEvent? GetExistingBuff(Entity entity, int spellId, int sourceId)
        => entity.Buffs.LastOrDefault(b => b.Ability.Id == spellId && b.SourceId == sourceId && b.End is null);

    private void FabricateStackChange(TrackedBuffEvent tracked, BuffEvent trigger, int oldStacks, int newStacks, bool isDebuff)
    {
        ChangeStackEvent change = isDebuff
            ? new ChangeDebuffStackEvent()
            : new ChangeBuffStackEvent();

        change.Timestamp = trigger.Timestamp;
        change.Ability = trigger.Ability;
        change.SourceId = trigger.SourceId;
        change.TargetId = trigger.TargetId;
        change.Start = tracked.Start;
        change.End = tracked.End;
        change.IsDebuff = isDebuff;
        change.OldStacks = oldStacks;
        change.NewStacks = newStacks;
        change.Stacks = newStacks;
        change.StacksGained = newStacks - oldStacks;
        change.StackHistory.Add(new ChangeStackEvent.History(newStacks, trigger.Timestamp));

        Owner.EventEmitter.FabricateEvent(change, trigger);
    }
}

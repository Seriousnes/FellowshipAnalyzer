using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Core module that tracks buff/debuff state for every unit seen in the event log, keyed by
/// <see cref="UnitKey"/> so distinct spawns of one actor id stay separate. Players seed from their
/// <see cref="CombatantInfoEvent"/> as <see cref="Combatant"/>s; any other unit a player-sourced aura
/// event targets is fabricated as an <see cref="Enemy"/>. Exposes <see cref="Selected"/> for downstream
/// modules that depend on the analyzed player, and aura-query methods for enemy debuff tracking.
/// </summary>
public sealed partial class Combatants : Analyzer
{
    private readonly Dictionary<UnitKey, Entity> _units = [];

    /// <summary>The combatant representing the selected (analyzed) player.</summary>
    public Combatant Selected { get; }

    /// <summary>Every tracked unit keyed by <see cref="UnitKey"/>.</summary>
    public IReadOnlyDictionary<UnitKey, Entity> Units => _units;

    public Combatants(ParseContext parseContext, IReadOnlyList<Event> events)
    {
        foreach (var e in events)
        {
            if (e is CombatantInfoEvent info)
            {
                var key = new UnitKey(info.SourceId, null);
                if (!_units.ContainsKey(key))
                    _units[key] = new Combatant(info);
            }
        }

        Selected = parseContext.SelectedCombatant;
        _units[new UnitKey(parseContext.PlayerId, null)] = parseContext.SelectedCombatant;

        foreach (var entity in _units.Values)
        {
            if (entity is not Combatant combatant) continue;

            foreach (var aura in combatant.Auras)
            {
                var prepullBuff = new TrackedBuffEvent
                {
                    Timestamp = combatant.Info.Timestamp,
                    Ability = new Ability { FSLID = aura.Ability, Name = aura.Name, Icon = aura.Icon },
                    SourceId = aura.Source,
                    TargetId = combatant.Id,
                    Start = combatant.Info.Timestamp,
                    Stacks = aura.Stacks,
                    IsDebuff = aura.Source != combatant.Id,
                };
                prepullBuff.StackHistory.Add(new TrackedBuffEvent.StackHistoryElement
                {
                    Stacks = aura.Stacks,
                    Timestamp = combatant.Info.Timestamp,
                });
                combatant.ApplyBuff(prepullBuff);
            }
        }
    }

    /// <summary>The tracked unit for an actor id and spawn instance, or null when none is tracked.</summary>
    public Entity? GetUnit(int actorId, int? instance) => _units.GetValueOrDefault(new UnitKey(actorId, instance));

    /// <summary>
    /// The number of distinct non-selected units with at least one active window of the effect at
    /// <paramref name="timestamp"/>, optionally restricted to auras applied by <paramref name="sourceId"/>.
    /// </summary>
    public int CountEnemiesWithAura(int effectId, long timestamp, int? sourceId = null)
        => EnemiesWithAura(effectId, timestamp, sourceId).Count;

    /// <summary>
    /// The keys of every non-selected unit with at least one active window of the effect at
    /// <paramref name="timestamp"/>, optionally restricted to auras applied by <paramref name="sourceId"/>.
    /// </summary>
    public IReadOnlyCollection<UnitKey> EnemiesWithAura(int effectId, long timestamp, int? sourceId = null)
    {
        var keys = new List<UnitKey>();
        foreach (var (key, entity) in _units)
        {
            if (entity is Enemy && entity.GetAuraInstanceCount(effectId, timestamp, sourceId) > 0)
                keys.Add(key);
        }
        return keys;
    }

    /// <summary>
    /// The number of concurrently-open windows of the effect active on a unit at <paramref name="timestamp"/>,
    /// optionally restricted to auras applied by <paramref name="sourceId"/>. Returns 0 when the unit is
    /// not tracked.
    /// </summary>
    public int AuraInstanceCount(int actorId, int? instance, int effectId, long timestamp, int? sourceId = null)
        => GetUnit(actorId, instance)?.GetAuraInstanceCount(effectId, timestamp, sourceId) ?? 0;

    /// <summary>
    /// The stacks summed across every concurrently-open window of the effect on a unit at
    /// <paramref name="timestamp"/>, optionally restricted to auras applied by <paramref name="sourceId"/>.
    /// Returns 0 when the unit is not tracked. Read it while the parse is dispatching, since a window
    /// carries its live stack count rather than its count at <paramref name="timestamp"/>.
    /// </summary>
    public int AuraStackSum(int actorId, int? instance, int effectId, long timestamp, int? sourceId = null)
        => GetUnit(actorId, instance)?.GetAuraStackSum(effectId, timestamp, sourceId) ?? 0;

    /// <summary>
    /// Every window of an effect standing on a non-selected unit that overlaps
    /// <paramref name="from"/>..<paramref name="to"/>, each clipped to that range and the whole
    /// ordered by start. Windows are historical, so this reads correctly after the parse; use it to
    /// reconstruct how an effect layered across a slice of the fight.
    /// </summary>
    public IReadOnlyList<AuraWindow> EnemyAuraWindows(int effectId, int from, int to, int? sourceId = null)
    {
        var windows = new List<AuraWindow>();
        foreach (var entity in _units.Values)
        {
            if (entity is not Enemy) continue;
            windows.AddRange(entity.GetAuraWindows(effectId, from, to, sourceId));
        }
        windows.Sort((a, b) => a.Start.CompareTo(b.Start));
        return windows;
    }

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

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent e)
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

    private Entity GetOrCreateEntity(int targetId, int? instance)
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

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Core module that tracks buff/debuff state for all combatants seen in the event log.
/// Populates <see cref="Combatant.Buffs"/> throughout event dispatch and exposes
/// <see cref="Selected"/> for downstream modules that depend on the analyzed player.
/// </summary>
public sealed partial class Combatants : Analyzer
{
    private readonly Dictionary<int, Combatant> _combatants = [];

    /// <summary>The combatant representing the selected (analyzed) player.</summary>
    public Combatant? Selected { get; }

    public IReadOnlyDictionary<int, Combatant> All => _combatants;

    public Combatants(ParseContext parseContext, IReadOnlyList<Event> events)
    {
        foreach (var e in events)
        {
            if (e is CombatantInfoEvent info && !_combatants.ContainsKey(info.SourceId))
                _combatants[info.SourceId] = new Combatant(info);
        }

        if (_combatants.TryGetValue(parseContext.PlayerId, out var selected))
            Selected = selected;

        foreach (var (_, combatant) in _combatants)
        {
            foreach (var aura in combatant.Auras)
            {
                var prepullBuff = new TrackedBuffEvent
                {
                    Timestamp = combatant.Info.Timestamp,
                    Ability = new Ability { Guid = aura.Ability, Name = aura.Name, Icon = aura.Icon },
                    SourceId = aura.Source,
                    TargetId = combatant.Id,
                    Start = combatant.Info.Timestamp,
                    Stacks = aura.Stacks,
                    IsDebuff = false,
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

    [On<ApplyBuffEvent>]
    private void OnApplyBuff(ApplyBuffEvent e) => ApplyBuff(e, isDebuff: false);

    [On<ApplyDebuffEvent>]
    private void OnApplyDebuff(ApplyDebuffEvent e) => ApplyBuff(e, isDebuff: true);

    private void ApplyBuff(BuffEvent e, bool isDebuff)
    {
        var entity = GetOrCreateEntity(e.TargetId);

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
        var entity = GetOrCreateEntity(e.TargetId);
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
        var entity = GetOrCreateEntity(e.TargetId);
        GetExistingBuff(entity, e.Ability.Id, e.SourceId)?.RefreshHistory.Add(e.Timestamp);
    }

    [On<RefreshDebuffEvent>]
    private void OnRefreshDebuff(RefreshDebuffEvent e)
    {
        var entity = GetOrCreateEntity(e.TargetId);
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
        var entity = GetOrCreateEntity(e.TargetId);
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

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent e)
    {
        foreach (var combatant in _combatants.Values)
        {
            foreach (var buff in combatant.Buffs.Where(b => b.End is null))
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

    private Combatant GetOrCreateEntity(int targetId)
    {
        if (!_combatants.TryGetValue(targetId, out var entity))
        {
            var shell = new CombatantInfoEvent { SourceId = targetId };
            entity = new Combatant(shell);
            _combatants[targetId] = entity;
        }
        return entity;
    }

    private static TrackedBuffEvent? GetExistingBuff(Combatant entity, int spellId, int sourceId)
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

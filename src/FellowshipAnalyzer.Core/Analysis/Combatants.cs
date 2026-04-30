using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Core module that tracks buff/debuff state for all combatants seen in the event log.
/// Populates <see cref="Combatant.Buffs"/> throughout event dispatch and sets
/// <see cref="CombatLogParser.SelectedCombatant"/> before other modules initialize.
/// </summary>
public sealed class Combatants : Analyzer
{
    private readonly Dictionary<int, Combatant> _combatants = [];

    /// <summary>The combatant representing the selected (analyzed) player.</summary>
    public Combatant? Selected { get; private set; }

    public IReadOnlyDictionary<int, Combatant> All => _combatants;

    public override void Initialize()
    {
        // Pre-scan events to build Combatant instances before dispatch begins.
        foreach (var e in Owner.Events.OfType<CombatantInfoEvent>())
        {
            if (!_combatants.ContainsKey(e.SourceId))
                _combatants[e.SourceId] = new Combatant(e);
        }

        if (_combatants.TryGetValue(Owner.PlayerId, out var selected))
        {
            Selected = selected;
            Owner.SelectedCombatant = selected;
        }

        // Seed prepull auras as open TrackedBuffEvents.
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

        AddEventListener(Events.ApplyBuff, OnApplyBuff);
        AddEventListener(Events.ApplyDebuff, OnApplyDebuff);
        AddEventListener(Events.RemoveBuff, OnRemoveBuff);
        AddEventListener(Events.RemoveDebuff, OnRemoveDebuff);
        AddEventListener(Events.RefreshBuff, OnRefreshBuff);
        AddEventListener(Events.RefreshDebuff, OnRefreshDebuff);
        AddEventListener(Events.ApplyBuffStack, OnApplyBuffStack);
        AddEventListener(Events.RemoveBuffStack, OnRemoveBuffStack);
        AddEventListener(Events.ApplyDebuffStack, OnApplyDebuffStack);
        AddEventListener(Events.RemoveDebuffStack, OnRemoveDebuffStack);
        AddEventListener(Events.FightEnd, OnFightEnd);
    }

    // -------------------------------------------------------------------------
    // Apply
    // -------------------------------------------------------------------------

    private void OnApplyBuff(ApplyBuffEvent e) => ApplyBuff(e, isDebuff: false);
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

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    private void OnRemoveBuff(RemoveBuffEvent e) => RemoveBuff(e, isDebuff: false);
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
            // Buff was active before the fight started and never received an apply event.
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

    // -------------------------------------------------------------------------
    // Refresh
    // -------------------------------------------------------------------------

    private void OnRefreshBuff(RefreshBuffEvent e)
    {
        var entity = GetOrCreateEntity(e.TargetId);
        GetExistingBuff(entity, e.Ability.Id, e.SourceId)?.RefreshHistory.Add(e.Timestamp);
    }

    private void OnRefreshDebuff(RefreshDebuffEvent e)
    {
        var entity = GetOrCreateEntity(e.TargetId);
        GetExistingBuff(entity, e.Ability.Id, e.SourceId)?.RefreshHistory.Add(e.Timestamp);
    }

    // -------------------------------------------------------------------------
    // Stack changes
    // -------------------------------------------------------------------------

    private void OnApplyBuffStack(ApplyBuffStackEvent e) => UpdateStack(e, isDebuff: false);
    private void OnRemoveBuffStack(RemoveBuffStackEvent e) => UpdateStack(e, isDebuff: false);
    private void OnApplyDebuffStack(ApplyDebuffStackEvent e) => UpdateStack(e, isDebuff: true);
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

    // -------------------------------------------------------------------------
    // Fight end — close all open buffs
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Combatant GetOrCreateEntity(int targetId)
    {
        if (!_combatants.TryGetValue(targetId, out var entity))
        {
            // No CombatantInfoEvent for this actor — create a shell.
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

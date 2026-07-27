using System.Linq.Expressions;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

using static FellowshipAnalyzer.Core.Analysis.Analyzer;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Accumulates criteria expressions and combines them with logical AND into a single compiled
/// predicate that <see cref="EventEmitter"/> tests against each dispatched event.
/// </summary>
public abstract class EventFilter
{
    private readonly List<Expression<Func<Event, bool>>> _conditions = [];
    private Func<Event, bool>? _compiled;

    internal CombatLogParser Owner { get; private set; } = null!;

    /// <summary>Adds an expression that must also hold for an event to pass this filter.</summary>
    public void AddCriteria(Expression<Func<Event, bool>> expression) => _conditions.Add(expression);

    /// <summary>
    /// Combines every criterion added so far into a single predicate and compiles it, caching the
    /// result so repeated calls do not recompile. <paramref name="owner"/> is captured as
    /// <see cref="Owner"/> so <c>By</c>/<c>To</c> criteria can resolve the current player.
    /// </summary>
    public Func<Event, bool> Compile(CombatLogParser owner)
    {
        Owner = owner;

        if (_compiled is null)
        {
            var combined = _conditions[0];
            foreach (var condition in _conditions.Skip(1))
            {
                combined = AndAlso(combined, condition);
            }

            _compiled = combined.Compile();
        }

        return _compiled;
    }

    private static Expression<Func<Event, bool>> AndAlso(
        Expression<Func<Event, bool>> left,
        Expression<Func<Event, bool>> right)
    {
        var visitor = new ReplaceParameterVisitor(right.Parameters[0], left.Parameters[0]);
        var rewritten = (Expression<Func<Event, bool>>)visitor.Visit(right);
        var body = Expression.AndAlso(left.Body, rewritten.Body);
        return Expression.Lambda<Func<Event, bool>>(body, left.Parameters);
    }
}

/// <summary>A filter that matches every dispatched event regardless of its concrete type.</summary>
public class AnyEventFilter : EventFilter<Event>
{
    /// <summary>Matches unconditionally; every event passes.</summary>
    protected override Expression<Func<Event, bool>> GetInitialCriteria() => static e => true;
}

/// <summary>
/// Fluent builder for a compiled predicate over events of type <typeparamref name="T"/>. Starts
/// from a type check and narrows with <see cref="By"/>, <see cref="To"/>, <see cref="Spell"/>, and
/// <see cref="ExtraSpell"/>.
/// </summary>
public class EventFilter<T> : EventFilter where T : Event
{
    /// <summary>Creates a filter seeded with <see cref="GetInitialCriteria"/> as its first criterion.</summary>
    public EventFilter()
    {
        AddCriteria(GetInitialCriteria());
    }

    /// <summary>The starting criterion for this filter: that the event is a <typeparamref name="T"/>.</summary>
    protected virtual Expression<Func<Event, bool>> GetInitialCriteria() => static e => e is T;

    /// <summary>
    /// Narrows the filter to events sourced from the selected player and/or their pet, per the
    /// <c>SELECTED_PLAYER</c> / <c>SELECTED_PLAYER_PET</c> flags in <paramref name="by"/>.
    /// </summary>
    public EventFilter<T> By(int by)
    {
        if (!ValidateBy(by))
        {
            throw new ArgumentOutOfRangeException(nameof(by), $"By filter not recognized: {by}");
        }

        var byCriteria = GetByCheck(by);
        if (byCriteria is not null)
        {
            AddCriteria(byCriteria);
        }

        return this;
    }

    /// <summary>
    /// Narrows the filter to events targeting the selected player and/or their pet, per the
    /// <c>SELECTED_PLAYER</c> / <c>SELECTED_PLAYER_PET</c> flags in <paramref name="to"/>.
    /// </summary>
    public EventFilter<T> To(int to)
    {
        var toCriteria = GetToCheck(to);
        if (toCriteria is not null)
        {
            AddCriteria(toCriteria);
        }

        return this;
    }

    /// <summary>Narrows the filter to events whose <see cref="IAbilityEvent.Ability"/> is one of <paramref name="spells"/>.</summary>
    public EventFilter<T> Spell(params Spell[] spells)
    {
        var ids = spells.Select(s => s.FSLID).ToArray();
        AddCriteria(e => e is IAbilityEvent && ids.Contains(((IAbilityEvent)e).Ability.Id));
        return this;
    }

    /// <summary>Narrows the filter to events whose <see cref="IExtraAbilityEvent.ExtraAbility"/> is one of <paramref name="spells"/>.</summary>
    public EventFilter<T> ExtraSpell(params Spell[] spells)
    {
        var ids = spells.Select(s => s.FSLID).ToArray();
        AddCriteria(e => e is IExtraAbilityEvent && ids.Contains(((IExtraAbilityEvent)e).ExtraAbility.Id));
        return this;
    }

    private Expression<Func<Event, bool>>? GetByCheck(int by)
    {
        var checkPlayer = (by & SELECTED_PLAYER) != 0;
        var checkPet = (by & SELECTED_PLAYER_PET) != 0;

        if (checkPlayer && checkPet)
        {
            return e => e is IHasSourceEvent && (Owner.ByPlayer((IHasSourceEvent)e, null) || Owner.ByPlayerPet((IHasSourceEvent)e));
        }

        if (checkPlayer)
        {
            return e => e is IHasSourceEvent && Owner.ByPlayer((IHasSourceEvent)e, null);
        }

        if (checkPet)
        {
            return e => e is IHasSourceEvent && Owner.ByPlayerPet((IHasSourceEvent)e);
        }

        return null;
    }

    private Expression<Func<Event, bool>>? GetToCheck(int to)
    {
        var checkPlayer = (to & SELECTED_PLAYER) != 0;
        var checkPet = (to & SELECTED_PLAYER_PET) != 0;

        if (checkPlayer && checkPet)
        {
            return e => e is IHasTargetEvent && (Owner.ToPlayer((IHasTargetEvent)e, null) || Owner.ToPlayerPet((IHasTargetEvent)e));
        }

        if (checkPlayer)
        {
            return e => e is IHasTargetEvent && Owner.ToPlayer((IHasTargetEvent)e, null);
        }

        if (checkPet)
        {
            return e => e is IHasTargetEvent && Owner.ToPlayerPet((IHasTargetEvent)e);
        }

        return null;
    }

    private static bool ValidateBy(int value) => (value & (SELECTED_PLAYER | SELECTED_PLAYER_PET)) == value;
}

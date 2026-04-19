using System.Linq.Expressions;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

using static FellowshipAnalyzer.Core.Analysis.Analyzer;

namespace FellowshipAnalyzer.Core.Analysis;

public abstract class EventFilter
{
    private readonly List<Expression<Func<Event, bool>>> _conditions = [];
    private Func<Event, bool>? _compiled;

    internal CombatLogParser Owner { get; private set; } = null!;

    public void AddCriteria(Expression<Func<Event, bool>> expression) => _conditions.Add(expression);

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

public class AnyEventFilter : EventFilter<Event>
{
    protected override Expression<Func<Event, bool>> GetInitialCriteria() => static e => true;
}

public class EventFilter<T> : EventFilter where T : Event
{
    public EventFilter()
    {
        AddCriteria(GetInitialCriteria());
    }

    protected virtual Expression<Func<Event, bool>> GetInitialCriteria() => static e => e is T;

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

    public EventFilter<T> To(int to)
    {
        var toCriteria = GetToCheck(to);
        if (toCriteria is not null)
        {
            AddCriteria(toCriteria);
        }

        return this;
    }

    public EventFilter<T> Spell(params int[] spellIds)
    {
        AddCriteria(e => e is IAbilityEvent && spellIds.Contains(((IAbilityEvent)e).Ability.Id));
        return this;
    }

    private Expression<Func<Event, bool>>? GetByCheck(int by)
    {
        var checkPlayer = (by & SELECTED_PLAYER) != 0;
        var checkPet = (by & SELECTED_PLAYER_PET) != 0;

        if (checkPlayer && checkPet)
        {
            return e => e is IHasSourceEvent && (Owner.ByPlayer((IHasSourceEvent)e) || Owner.ByPlayerPet((IHasSourceEvent)e));
        }

        if (checkPlayer)
        {
            return e => e is IHasSourceEvent && Owner.ByPlayer((IHasSourceEvent)e);
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
            return e => e is IHasTargetEvent && (Owner.ToPlayer((IHasTargetEvent)e) || Owner.ToPlayerPet((IHasTargetEvent)e));
        }

        if (checkPlayer)
        {
            return e => e is IHasTargetEvent && Owner.ToPlayer((IHasTargetEvent)e);
        }

        if (checkPet)
        {
            return e => e is IHasTargetEvent && Owner.ToPlayerPet((IHasTargetEvent)e);
        }

        return null;
    }

    private static bool ValidateBy(int value) => (value & (SELECTED_PLAYER | SELECTED_PLAYER_PET)) == value;
}

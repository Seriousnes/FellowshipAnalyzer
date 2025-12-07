using FellowshipAnalyzer.Core.Events;

using static FellowshipAnalyzer.Core.Analysis.Analyzer;

namespace FellowshipAnalyzer.Core.Analysis;

public abstract class EventFilter
{
    private readonly List<Func<CombatLogParser, Event, bool>> _criteria = [];

    protected void AddCriteria(Func<CombatLogParser, Event, bool> criterion) => _criteria.Add(criterion);

    public Func<Event, bool> Build(CombatLogParser owner)
    {
        var snapshot = _criteria.ToArray();
        return e =>
        {
            foreach (var criterion in snapshot)
            {
                if (!criterion(owner, e))
                {
                    return false;
                }
            }

            return true;
        };
    }
}

public class AnyEventFilter : EventFilter<Event>
{
    protected override Func<CombatLogParser, Event, bool> GetInitialCriteria() => static (_, _) => true;
}

public class EventFilter<T> : EventFilter where T : Event
{
    public EventFilter()
    {
        AddCriteria(GetInitialCriteria());
    }

    protected virtual Func<CombatLogParser, Event, bool> GetInitialCriteria() => static (_, e) => e is T;

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
        AddCriteria((_, e) => e is IAbilityEvent ability && spellIds.Contains(ability.AbilityGameId));
        return this;
    }

    private static Func<CombatLogParser, Event, bool>? GetByCheck(int by)
    {
        var checkPlayer = (by & SELECTED_PLAYER) != 0;
        var checkPet = (by & SELECTED_PLAYER_PET) != 0;

        if (checkPlayer && checkPet)
        {
            return (pipeline, e) => e is IHasSourceEvent src && (pipeline.ByPlayer(src) || pipeline.ByPlayerPet(src));
        }

        if (checkPlayer)
        {
            return (pipeline, e) => e is IHasSourceEvent src && pipeline.ByPlayer(src);
        }

        if (checkPet)
        {
            return (pipeline, e) => e is IHasSourceEvent src && pipeline.ByPlayerPet(src);
        }

        return null;
    }

    private static Func<CombatLogParser, Event, bool>? GetToCheck(int to)
    {
        var checkPlayer = (to & SELECTED_PLAYER) != 0;
        var checkPet = (to & SELECTED_PLAYER_PET) != 0;

        if (checkPlayer && checkPet)
        {
            return (pipeline, e) => e is IHasTargetEvent tgt && (pipeline.ToPlayer(tgt) || pipeline.ToPlayerPet(tgt));
        }

        if (checkPlayer)
        {
            return (pipeline, e) => e is IHasTargetEvent tgt && pipeline.ToPlayer(tgt);
        }

        if (checkPet)
        {
            return (pipeline, e) => e is IHasTargetEvent tgt && pipeline.ToPlayerPet(tgt);
        }

        return null;
    }

    private static bool ValidateBy(int value) => (value & (SELECTED_PLAYER | SELECTED_PLAYER_PET)) == value;
}

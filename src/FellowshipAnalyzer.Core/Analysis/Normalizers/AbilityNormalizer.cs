using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Populates the <see cref="IAbilityEvent.Ability"/> property from
/// <see cref="IAbilityEvent.AbilityGameId"/> when <c>Ability</c> is null.
/// This handles the case where the FellowshipLogs GraphQL query returns
/// <c>abilityGameID</c> instead of the full <c>ability</c> object.
/// Runs before <see cref="CastLinkNormalizer"/>.
/// </summary>
public sealed class AbilityNormalizer : IEventNormalizer
{
    public int Priority => -100;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        foreach (var e in events)
        {
            if (e is IAbilityEvent { Ability: null, AbilityGameId: > 0 } abilityEvent)
            {
                var spell = SpellRegistry.MaybeGet(abilityEvent.AbilityGameId);
                abilityEvent.Ability = spell is not null
                    ? new Ability { Guid = spell.Id, Name = spell.Name, Icon = spell.Icon }
                    : new Ability { Guid = abilityEvent.AbilityGameId };
            }
        }

        return events;
    }
}

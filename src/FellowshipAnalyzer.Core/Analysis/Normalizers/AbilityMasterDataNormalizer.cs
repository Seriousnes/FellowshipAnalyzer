using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Populates <see cref="IAbilityEvent.Ability"/> and <see cref="IExtraAbilityEvent.ExtraAbility"/>
/// on events from report master data, then stamps each resolved ability with the damage school the
/// game data gives it.
/// <para>
/// Runs at <see cref="Priority"/> -100, before <see cref="CastLinkNormalizer"/> (priority 0),
/// because that normalizer reads <c>Ability.FSLID</c> for cast matching.
/// </para>
/// <para>
/// The school is assigned on every ability event rather than only on a freshly resolved one, so an
/// ability that arrived already populated - from a cached preload, say - still ends up carrying the
/// school this build derives rather than whatever an older one stored.
/// </para>
/// </summary>
public sealed class AbilityMasterDataNormalizer(ReportMasterDataService masterData) : IEventNormalizer
{
    /// <inheritdoc/>
    public int Priority => -100;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        foreach (var e in events)
        {
            if (e is IAbilityEvent abilityEvent && abilityEvent.AbilityGameId != 0)
            {
                if (abilityEvent.Ability is null or { FSLID.Value: 0 })
                    abilityEvent.Ability = masterData.GetAbility(abilityEvent.AbilityGameId);
                abilityEvent.Ability.Type = SpellSchools.For(abilityEvent.AbilityGameId);
            }

            if (e is IExtraAbilityEvent extraAbilityEvent && extraAbilityEvent.ExtraAbilityGameId != 0)
            {
                if (extraAbilityEvent.ExtraAbility is null or { FSLID.Value: 0 })
                    extraAbilityEvent.ExtraAbility = masterData.GetAbility(extraAbilityEvent.ExtraAbilityGameId);
                extraAbilityEvent.ExtraAbility.Type = SpellSchools.For(extraAbilityEvent.ExtraAbilityGameId);
            }
        }

        return events;
    }
}

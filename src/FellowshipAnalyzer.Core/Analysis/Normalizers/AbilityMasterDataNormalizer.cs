using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Populates <see cref="IAbilityEvent.Ability"/> and <see cref="IExtraAbilityEvent.ExtraAbility"/>
/// on events from report master data.
/// <para>
/// Runs at <see cref="Priority"/> -100, before <see cref="CastLinkNormalizer"/> (priority 0),
/// because that normalizer reads <c>Ability.FSLID</c> for cast matching.
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
            if (e is IAbilityEvent abilityEvent && abilityEvent.AbilityGameId != 0 && abilityEvent.Ability is null or { FSLID.Value: 0 })
                abilityEvent.Ability = masterData.GetAbility(abilityEvent.AbilityGameId);

            if (e is IExtraAbilityEvent extraAbilityEvent && extraAbilityEvent.ExtraAbilityGameId != 0 && extraAbilityEvent.ExtraAbility is null or { FSLID.Value: 0 })
                extraAbilityEvent.ExtraAbility = masterData.GetAbility(extraAbilityEvent.ExtraAbilityGameId);
        }

        return events;
    }
}

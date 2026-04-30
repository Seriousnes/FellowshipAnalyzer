using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Scoped service that holds the loaded master data for the current report.
/// Call <see cref="Load"/> once after fetching master data, then use
/// <see cref="GetAbility"/> and <see cref="GetHero"/> during analysis.
/// </summary>
public sealed class ReportMasterDataService
{
    private IReadOnlyDictionary<int, Ability> _abilitiesByGameId = new Dictionary<int, Ability>();
    private IReadOnlyList<ReportActor> _actors = [];

    /// <summary>
    /// Populates the service from fetched master data.
    /// </summary>
    public void Load(ReportMasterData masterData)
    {
        _actors = masterData.Actors;

        var dict = new Dictionary<int, Ability>(masterData.Abilities.Count);
        foreach (var ability in masterData.Abilities)
            dict[ability.Guid] = ability;

        _abilitiesByGameId = dict;
    }

    /// <summary>
    /// Returns the <see cref="Ability"/> for the given ability game ID, or a minimal stub if not found.
    /// </summary>
    public Ability GetAbility(int gameId)
    {
        if (_abilitiesByGameId.TryGetValue(gameId, out var ability))
            return ability;

        // Return a minimal stub so downstream code can still use the ID.
        return new Ability { Guid = gameId };
    }

    /// <summary>
    /// Returns the <see cref="Hero"/> definition for the given player actor ID,
    /// derived from the actor's <c>SubType</c>. Returns <c>null</c> if the actor
    /// is unknown or its SubType does not match a supported hero.
    /// </summary>
    public Hero? GetHero(int playerId)
    {
        var actor = _actors.FirstOrDefault(a => a.Id == playerId);
        return Hero.TryParse(actor?.SubType, out var hero) ? hero : (Hero?)null;
    }
}

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Scoped service that holds the loaded master data for the current report.
/// Call <see cref="Load"/> once after fetching master data, then use
/// <see cref="GetAbility"/> and <see cref="GetHeroId"/> during analysis.
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
    /// Returns the hero ID string for the given player actor ID, derived from the actor's SubType.
    /// Returns <c>null</c> if the actor is not found or has no SubType.
    /// </summary>
    public string? GetHeroId(int playerId)
    {
        var actor = _actors.FirstOrDefault(a => a.Id == playerId);
        return actor?.SubType?.ToLowerInvariant();
    }
}

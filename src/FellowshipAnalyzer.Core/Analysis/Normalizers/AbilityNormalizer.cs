using System.Diagnostics;
using System.Reflection;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Validates that every <see cref="IAbilityEvent"/> has its <see cref="IAbilityEvent.Ability"/>
/// populated by the API. When <c>Ability</c> is absent (i.e. the API returned only
/// <c>abilityGameID</c>), this normalizer falls back to constructing an <see cref="Ability"/>
/// from the raw ID and emits a diagnostic warning so that the missing data is visible.
/// Runs before <see cref="CastLinkNormalizer"/>.
/// </summary>
public sealed class AbilityNormalizer : IEventNormalizer
{
    public int Priority => -100;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        foreach (var e in events)
        {
            if (e is not IAbilityEvent abilityEvent) continue;

            var ability = abilityEvent.Ability;

            if (ability is { Guid: > 0 })
            {
                // Ability fully populated — optionally enrich from SpellRegistry if name/icon are missing.
                var registry = SpellRegistry.MaybeGet(ability.Guid);
                if (registry is not null)
                {
                    if (string.IsNullOrEmpty(ability.Name) && !string.IsNullOrEmpty(registry.Name))
                        ability.Name = registry.Name;
                    if (string.IsNullOrEmpty(ability.Icon) && !string.IsNullOrEmpty(registry.Icon))
                        ability.Icon = registry.Icon;
                }
                continue;
            }

            // Ability missing — fall back to the raw abilityGameID for backward compat.
            // This happens when the API returns abilityGameID instead of the full ability object.
            var rawId = GetRawAbilityGameId(e);

            if (rawId <= 0)
            {
                Debug.WriteLine($"[AbilityNormalizer] {e.GetType().Name} at t={e.Timestamp} has neither Ability nor abilityGameID.");
                continue;
            }

            Debug.WriteLine($"[AbilityNormalizer] {e.GetType().Name} at t={e.Timestamp} is missing Ability; falling back to abilityGameID={rawId}. Update the API query to return the full ability object.");

            var spell = SpellRegistry.MaybeGet(rawId);
            abilityEvent.Ability = spell is not null
                ? new Ability { Guid = spell.Id, Name = spell.Name, Icon = spell.Icon }
                : new Ability { Guid = rawId };
        }

        return events;
    }

    /// <summary>
    /// Reads the raw <c>AbilityGameId</c> field from the concrete event record via reflection.
    /// The property is intentionally not on <see cref="IAbilityEvent"/> — it exists only to
    /// support JSON deserialization from the FellowshipLogs API.
    /// </summary>
    private static int GetRawAbilityGameId(Event e)
    {
        var prop = e.GetType().GetProperty("AbilityGameId", BindingFlags.Public | BindingFlags.Instance);
        return prop is not null ? (int)(prop.GetValue(e) ?? 0) : 0;
    }
}

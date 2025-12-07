using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Components.Timeline;

/// <summary>
/// Per-aura visibility/priority entry persisted in the user's timeline config.
/// </summary>
/// <param name="SpellId">The aura spell ID (combat-log <c>abilityGameID</c>).</param>
/// <param name="Visible">Whether the aura is shown on the Timeline aura bar.</param>
/// <param name="Priority">Vertical ordering priority. Lower = bottom (closest to cast bar).</param>
public sealed record AuraConfigEntry(int SpellId, bool Visible, int Priority);

/// <summary>
/// Per-cooldown visibility/order entry persisted in the user's timeline config.
/// </summary>
/// <param name="SpellId">The cooldown spell ID (combat-log <c>abilityGameID</c>).</param>
/// <param name="Visible">Whether the cooldown lane is shown on the Timeline.</param>
/// <param name="SortOrder">Vertical lane ordering. Lower = closer to the top of the cooldowns section.</param>
public sealed record CooldownConfigEntry(int SpellId, bool Visible, int SortOrder);

/// <summary>
/// Per-hero Timeline customization (which cooldown lanes / aura priority levels are visible
/// and in what order). Persisted to <c>localStorage</c> per hero.
/// </summary>
public sealed class TimelineConfig
{
    public List<AuraConfigEntry> Auras { get; init; } = [];
    public List<CooldownConfigEntry> Cooldowns { get; init; } = [];
}

/// <summary>
/// A spell option offered to the user in the Timeline settings modal. Combines the
/// combat-log <c>abilityGameID</c> with display metadata (name + icon URL fragment).
/// </summary>
/// <param name="SpellId">The combat-log <c>abilityGameID</c>.</param>
/// <param name="Name">The display name.</param>
/// <param name="IconUrl">The icon URL fragment (the <c>Icon</c> field on <c>Ability</c>/<c>Spell</c>).</param>
public sealed record TimelineSpellOption(int SpellId, string Name, string? IconUrl);

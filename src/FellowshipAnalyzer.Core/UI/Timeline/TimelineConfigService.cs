using System.Text.Json;
using Microsoft.JSInterop;

namespace FellowshipAnalyzer.Core.UI.Timeline;

/// <summary>
/// Loads/saves <see cref="TimelineConfig"/> per-hero from the browser's <c>localStorage</c>.
/// Resilient to JS-interop being unavailable during pre-rendering.
/// </summary>
public sealed class TimelineConfigService(IJSRuntime js)
{
    private const string KeyPrefix = "timeline-config:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string KeyFor(string heroKey) => KeyPrefix + heroKey;

    /// <summary>
    /// Loads the stored config for the given hero. Returns an empty config if nothing is
    /// stored, JS interop is unavailable (SSR / disconnected), or the stored value is invalid.
    /// </summary>
    public async Task<TimelineConfig> LoadAsync(string heroKey)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", KeyFor(heroKey));
            if (string.IsNullOrWhiteSpace(json))
            {
                return new TimelineConfig();
            }

            return JsonSerializer.Deserialize<TimelineConfig>(json, JsonOptions) ?? new TimelineConfig();
        }
        catch (InvalidOperationException)
        {
            return new TimelineConfig();
        }
        catch (JSDisconnectedException)
        {
            return new TimelineConfig();
        }
        catch (JsonException)
        {
            return new TimelineConfig();
        }
    }

    /// <summary>
    /// Saves the given config for the given hero. Silently no-ops if JS interop is unavailable.
    /// </summary>
    public async Task SaveAsync(string heroKey, TimelineConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", KeyFor(heroKey), json);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Merges the stored config with defaults derived from the analysis run:
    /// <list type="bullet">
    /// <item>Pre-configured auras (those the hero declared via <c>TimelineHighlight = true</c>) that
    /// aren't already in <paramref name="stored"/> are added with <c>Visible = true</c> and
    /// <c>Priority = 0</c>. Non-pre-configured in-log auras are NOT auto-added — they only enter the
    /// config when the user explicitly adds them via the settings modal.</item>
    /// <item>Cooldowns seen in the log but not in <paramref name="stored"/> are added visible by default.
    /// <c>SortOrder</c> is the spellbook entry's <c>TimelineSortIndex</c> when present, otherwise the
    /// cooldown's discovery order in the report.</item>
    /// </list>
    /// Existing entries in <paramref name="stored"/> are preserved as-is.
    /// </summary>
    public static TimelineConfig MergeWithDefaults(
        TimelineConfig stored,
        IEnumerable<int> allCooldownIds,
        IReadOnlySet<int> defaultHighlightedAuras,
        IReadOnlyDictionary<int, int?> cooldownDefaultSortIndices)
    {
        var auraMap = stored.Auras.ToDictionary(a => a.SpellId);
        foreach (var id in defaultHighlightedAuras)
        {
            if (!auraMap.ContainsKey(id))
            {
                auraMap[id] = new AuraConfigEntry(
                    SpellId: id,
                    Visible: true,
                    Priority: 0);
            }
        }

        var cdMap = stored.Cooldowns.ToDictionary(c => c.SpellId);
        var defaultOrder = 0;
        foreach (var id in allCooldownIds)
        {
            if (!cdMap.ContainsKey(id))
            {
                var sortIndex = cooldownDefaultSortIndices.TryGetValue(id, out var v) ? v : null;
                cdMap[id] = new CooldownConfigEntry(
                    SpellId: id,
                    Visible: true,
                    SortOrder: sortIndex ?? defaultOrder);
            }

            defaultOrder++;
        }

        return new TimelineConfig
        {
            Auras = [.. auraMap.Values],
            Cooldowns = [.. cdMap.Values],
        };
    }
}

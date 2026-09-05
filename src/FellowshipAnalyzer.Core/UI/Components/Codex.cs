using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Addresses into the Fellowship Codex: the page a link opens, the tooltip fragment a hover fetches,
/// and the art the CDN serves. Every codex and CDN address the app builds comes from here.
/// </summary>
public static class Codex
{
    /// <summary>The codex origin, serving both the browsable pages and the <c>/api</c> routes.</summary>
    public const string Origin = "https://codex.fellowshipanalyzer.com";

    /// <summary>
    /// The origin serving codex art. Every art file is a PNG under <c>/ui</c>, so
    /// <see cref="IconUrl(string)"/> normalises whatever extension its caller was given.
    /// </summary>
    public const string AssetOrigin = "https://cdn.codex.fellowshipanalyzer.com";

    /// <summary>The codex page for the entity at <paramref name="path"/>, for example <c>ability/1964</c>.</summary>
    public static string PageUrl(string path) => $"{Origin}/{path}";

    /// <summary>
    /// The <c>/api</c> route answering the entity's tooltip fragment, relative to <see cref="Origin"/>.
    /// <paramref name="query"/> supplies the values a tooltip needs, such as the hero an item is read for.
    /// </summary>
    public static string TooltipPath(string path, string? query = null) =>
        query is { Length: > 0 } ? $"api/{path}/tooltip?{query}" : $"api/{path}/tooltip";

    /// <summary>
    /// The CDN address of <paramref name="icon"/>, an art name as the game data or a combat log writes
    /// it. Any directory in the name is dropped and any extension becomes <c>.png</c>.
    /// </summary>
    public static string IconUrl(string icon) => $"{AssetOrigin}/ui/{ArtName(icon)}.png";

    /// <summary>
    /// The CDN address of an item or gem's art at rarity <paramref name="tier"/>. Art the build draws
    /// once per rung has that rung's border, and its file ends in the name the build stores for the
    /// tier; art shared across every rung is addressed by its bare name.
    /// </summary>
    public static string IconUrl(string icon, int tier)
    {
        var art = ArtName(icon);
        var rarity = ItemRarities.NameFor(tier);

        return rarity.Length > 0 && ItemArt.IsDrawnPerRung(art)
            ? $"{AssetOrigin}/ui/{art}-{rarity.ToLowerInvariant()}.png"
            : $"{AssetOrigin}/ui/{art}.png";
    }

    private static ReadOnlySpan<char> ArtName(string icon)
    {
        var name = icon.AsSpan();

        var slash = name.LastIndexOfAny('/', '\\');
        if (slash >= 0)
            name = name[(slash + 1)..];

        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[..dot] : name;
    }
}

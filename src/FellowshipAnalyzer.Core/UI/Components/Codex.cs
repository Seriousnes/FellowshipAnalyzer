using Fellowship.SDK.Client;

using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Addresses into the Fellowship Codex. <see cref="CodexAddresses"/> writes the page a link opens and
/// the tooltip fragment a hover fetches; the textures the codex serves are addressed through
/// <see cref="CodexTextureAddresses"/>, because which file an icon name resolves to is read from
/// <c>spelldb.json</c>.
/// </summary>
public static class Codex
{
    private static string _origin = CodexOptions.Default.Origin;

    static Codex() => Use(CodexOptions.Default);

    /// <summary>Addresses the codex <paramref name="options"/> names, for the life of the app.</summary>
    public static void Use(CodexOptions options)
    {
        _origin = options.Origin;
        CodexTextureAddresses.Origin = options.TextureOrigin;
        CodexTextureAddresses.DungeonIconFor = Dungeons.IconFor;
    }

    /// <summary>The codex origin, serving both the browsable pages and the <c>/api</c> routes.</summary>
    public static string Origin => _origin;

    /// <summary>The origin serving codex textures.</summary>
    public static string TextureOrigin => CodexTextureAddresses.Origin;

    /// <summary>The codex page for the entity at <paramref name="path"/>, for example <c>ability/1964</c>.</summary>
    public static string PageUrl(string path) => $"{Origin}/{path}";

    /// <summary>The address of <paramref name="icon"/>, a texture name as the game data or a combat log writes it.</summary>
    public static string IconUrl(string icon) => CodexTextureAddresses.IconUrl(icon);

    /// <summary>
    /// The address of an item or gem's texture at rarity <paramref name="tier"/>. A texture the build
    /// draws once per rung has that rung's border, and its file ends in the name the build stores for
    /// the tier; a texture shared across every rung is addressed by its bare name.
    /// </summary>
    public static string IconUrl(string icon, int tier)
    {
        var texture = CodexTextureAddresses.TextureName(icon);
        var rarity = ItemRarities.NameFor(tier);

        return rarity.Length > 0 && ItemTexture.IsDrawnPerRung(texture)
            ? CodexTextureAddresses.IconUrl($"{texture}-{rarity.ToLowerInvariant()}")
            : CodexTextureAddresses.IconUrl(texture);
    }
}

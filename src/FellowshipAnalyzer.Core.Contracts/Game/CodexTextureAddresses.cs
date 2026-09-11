namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// Addresses into the textures the Fellowship Codex serves, so a record can address its own icon. The
/// app states these at startup from the codex it is configured to read.
/// </summary>
public static class CodexTextureAddresses
{
    private const string Size = "full";

    private static string _origin = string.Empty;

    /// <summary>The origin serving codex textures. A trailing slash is dropped when it is stated.</summary>
    public static string Origin
    {
        get => _origin;
        set => _origin = value.TrimEnd('/');
    }

    /// <summary>
    /// The texture a dungeon is drawn with, by the id a report writes as its zone, answering
    /// <see langword="null"/> when the game data declares no such dungeon.
    /// </summary>
    public static Func<int, string?> DungeonIconFor { get; set; } = _ => null;

    /// <summary>
    /// The address of <paramref name="icon"/>, a texture name as the game data or a combat log writes
    /// it. Any directory in the name is dropped and the size is written into the file name.
    /// </summary>
    public static string IconUrl(string icon) => IconUrl(TextureName(icon));

    /// <summary>The address of the already-normalised texture name <paramref name="texture"/>.</summary>
    public static string IconUrl(ReadOnlySpan<char> texture) => $"{Origin}/ui/{texture}_{Size}.webp";

    /// <summary><paramref name="icon"/> with any directory and extension removed.</summary>
    public static ReadOnlySpan<char> TextureName(string icon)
    {
        var name = icon.AsSpan();

        var slash = name.LastIndexOfAny('/', '\\');
        if (slash >= 0)
            name = name[(slash + 1)..];

        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[..dot] : name;
    }
}

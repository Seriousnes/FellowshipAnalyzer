using Fellowship.SDK.Client;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>Which codex the app addresses, and which origin serves that codex's textures.</summary>
public sealed class CodexOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Codex";

    /// <summary>The codex the app addresses when nothing configures another.</summary>
    public static CodexOptions Default { get; } = new();

    private string _origin = Trim(CodexAddresses.Origin);
    private string _textureOrigin = Trim(CodexAddresses.ArtOrigin);

    /// <summary>The codex origin, serving both the browsable pages and the <c>/api</c> routes.</summary>
    public string Origin
    {
        get => _origin;
        init => _origin = value.TrimEnd('/');
    }

    /// <summary>The origin serving codex textures.</summary>
    public string TextureOrigin
    {
        get => _textureOrigin;
        init => _textureOrigin = value.TrimEnd('/');
    }

    private static string Trim(Uri origin) => origin.ToString().TrimEnd('/');
}

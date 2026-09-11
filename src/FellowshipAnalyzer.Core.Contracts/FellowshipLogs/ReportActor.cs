using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Represents a player or NPC actor in a report.
/// </summary>
public sealed record ReportActor(
    int Id,
    string Name,
    string Type,
    string? SubType,
    string? Server,
    string? Icon
)
{
    private const string NpcIconPrefix = "custom-icon-";
    private const string BlueprintPrefix = "BP_";

    /// <summary>
    /// Icon URL for this actor, served from the Fellowship Codex, or <see langword="null"/> when the
    /// actor has no icon.
    /// </summary>
    public string? IconUrl =>
        Icon is { Length: > 0 } icon
        && icon.StartsWith(NpcIconPrefix, StringComparison.Ordinal)
        && icon[NpcIconPrefix.Length..] is { Length: > 0 } texture
        && !texture.StartsWith(BlueprintPrefix, StringComparison.Ordinal)
            ? CodexTextureAddresses.IconUrl(texture)
            : null;
}

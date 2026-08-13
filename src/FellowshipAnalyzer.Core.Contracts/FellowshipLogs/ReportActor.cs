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
    /// <summary>
    /// Icon URL for this actor, served from the RPGLogs CDN, or <see langword="null"/> when the
    /// actor carries no icon.
    /// </summary>
    public string? IconUrl =>
        string.IsNullOrEmpty(Icon)
            ? null
            : $"https://assets.rpglogs.com/img/fellowship/abilities/{Icon}";
}

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Metadata about a combat log report: title, timing, dungeons, and actors.
/// </summary>
public sealed record ReportInfo(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    List<ReportDungeon> Dungeons,
    List<ReportActor> Actors
)
{
    private const string PlayerActorType = "Player";

    /// <summary>
    /// Icon URL of the first non-player actor named <paramref name="name"/> that has an icon, or
    /// <see langword="null"/> when there is none. Names are matched case-insensitively; a boss and the
    /// dungeon it headlines share a name. Reports may contain unnamed actors, so a
    /// <see langword="null"/> or empty <paramref name="name"/> never matches.
    /// </summary>
    public string? FindNpcIconUrl(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        foreach (var actor in Actors)
        {
            if (string.Equals(actor.Type, PlayerActorType, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(actor.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (actor.IconUrl is { } iconUrl)
                return iconUrl;
        }

        return null;
    }
}

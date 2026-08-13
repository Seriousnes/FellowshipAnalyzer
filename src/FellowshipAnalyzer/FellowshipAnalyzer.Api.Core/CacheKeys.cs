namespace FellowshipAnalyzer.Api.Core;

internal static class CacheKeys
{
    public static string Analysis(string reportCode, int? dungeonId = null) =>
        dungeonId is { } id
            ? $"analysis:{reportCode.Trim()}:{id}"
            : $"analysis:{reportCode.Trim()}";

    public static string Character(int characterId) => $"character:{characterId}";

    public static string Events(string reportCode, int playerId, int dungeonId)
    {
        return $"events:{reportCode.Trim()}:{dungeonId}:{playerId}";
    }

    public static string BlobAnalysis(string reportCode, int? dungeonId = null)
        => dungeonId is { } id
            ? $"analysis/{reportCode.Trim()}/{id}"
            : $"analysis/{reportCode.Trim()}";

    public static string BlobCharacter(int characterId)
        => $"character/{characterId}";

    public static string BlobEvents(string reportCode, int playerId, int dungeonId)
        => $"events/{reportCode.Trim()}/{dungeonId}/{playerId}";

    public static string BlobDeaths(string reportCode, int dungeonId)
        => $"deaths/{reportCode.Trim()}/{dungeonId}";
}

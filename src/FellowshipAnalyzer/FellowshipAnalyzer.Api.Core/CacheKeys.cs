namespace FellowshipAnalyzer.Api.Core;

internal static class CacheKeys
{
    public static string Analysis(string reportCode) => $"analysis:{reportCode.Trim()}";

    public static string Character(int characterId) => $"character:{characterId}";

    public static string Events(string reportCode, int playerId, int fightId)
    {
        return $"events:{reportCode.Trim()}:{fightId}:{playerId}";
    }
}

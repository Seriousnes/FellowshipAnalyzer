namespace FellowshipAnalyzer.Api.Core;

internal static class CacheKeys
{
    // ── In-process (IMemoryCache) keys ──────────────────────────────────────

    public static string Analysis(string reportCode) => $"analysis:{reportCode.Trim()}";

    public static string Character(int characterId) => $"character:{characterId}";

    public static string Events(string reportCode, int playerId, int fightId)
    {
        return $"events:{reportCode.Trim()}:{fightId}:{playerId}";
    }

    // ── Blob (IPersistentCache) keys ─────────────────────────────────────────
    // Blob names must be URL-safe; using lowercase and hyphens only.

    public static string BlobAnalysis(string reportCode)
        => $"analysis/{reportCode.Trim().ToLowerInvariant()}";

    public static string BlobCharacter(int characterId)
        => $"character/{characterId}";

    public static string BlobEvents(string reportCode, int playerId, int fightId)
        => $"events/{reportCode.Trim().ToLowerInvariant()}/{fightId}/{playerId}";
}

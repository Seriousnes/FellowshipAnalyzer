using System.Globalization;

namespace FellowshipAnalyzer.SpellData;

/// <summary>Resolved absolute paths to the committed game-data export and the curated files beside it.</summary>
public static class SourcePaths
{
    public static readonly string RepoRoot = FindRepoRoot();

    public static readonly string ExportRoot = FindExportRoot();

    public static string Entities => Path.Combine(ExportRoot, "entities.jsonl");
    public static string Settings => Path.Combine(ExportRoot, "settings.json");
    public static string Overrides => Path.Combine(RepoRoot, "data", "overrides.json");
    public static string SpellDb => Path.Combine(RepoRoot, "data", "spelldb.json");

    private static string FindRepoRoot()
    {
        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(startDir);
            while (dir is not null)
            {
                if (dir.GetFiles("*.slnx").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException("Could not find repository root (no .slnx file found).");
    }

    private static string FindExportRoot()
    {
        var dataDir = Path.Combine(RepoRoot, "data");
        var best = Directory.Exists(dataDir)
            ? Directory.EnumerateDirectories(dataDir, "v*")
                .Select(dir => (Dir: dir, Name: Path.GetFileName(dir)))
                .Select(candidate => (
                    candidate.Dir,
                    Parsed: long.TryParse(
                        candidate.Name.AsSpan(1),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var build) ? build : (long?)null))
                .Where(candidate => candidate.Parsed is not null)
                .OrderByDescending(candidate => candidate.Parsed!.Value)
                .Select(candidate => candidate.Dir)
                .FirstOrDefault()
            : null;

        return best ?? throw new InvalidOperationException(
            $"Could not find a game-data export folder (no 'v<build>' directory under '{dataDir}').");
    }
}

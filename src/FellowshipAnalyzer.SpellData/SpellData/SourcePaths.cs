namespace FellowshipAnalyzer.SpellData;

/// <summary>Resolved absolute paths to the committed upstream game-data files.</summary>
public static class SourcePaths
{
    public static readonly string RepoRoot = FindRepoRoot();

    public static string SpellData => Path.Combine(RepoRoot, "external", "fs_tc_uploads", "s3", "spell_data.json");
    public static string GearData => Path.Combine(RepoRoot, "external", "fs_tc_uploads", "s3", "gear_data.json");
    public static string HeroData => Path.Combine(RepoRoot, "external", "fs_tc_uploads", "s3", "hero_data.json");
    public static string Abilities => Path.Combine(RepoRoot, "abilities.json");
    public static string Overrides => Path.Combine(RepoRoot, "data", "overrides.json");
    public static string SpellDb => Path.Combine(RepoRoot, "data", "spelldb.json");
    public static string DevNameMappings => Path.Combine(RepoRoot, "external", "fs_tc_uploads", "dev_name_mappings.md");

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
}

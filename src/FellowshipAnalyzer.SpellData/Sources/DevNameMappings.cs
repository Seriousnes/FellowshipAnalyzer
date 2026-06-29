using System.Text.RegularExpressions;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>Display-name lookup tables parsed from the Python dict blocks in <c>dev_name_mappings.md</c>.</summary>
public sealed class DevNameMappings
{
    public IReadOnlyDictionary<string, string> HeroNames { get; }
    public IReadOnlyDictionary<string, string> WeaponNames { get; }
    public IReadOnlyDictionary<string, string> WeaponTraitNames { get; }

    private DevNameMappings(
        Dictionary<string, string> heroNames,
        Dictionary<string, string> weaponNames,
        Dictionary<string, string> weaponTraitNames)
    {
        HeroNames = heroNames;
        WeaponNames = weaponNames;
        WeaponTraitNames = weaponTraitNames;
    }

    public static DevNameMappings Load(string path)
    {
        if (!File.Exists(path))
            return new DevNameMappings([], [], []);

        var text = File.ReadAllText(path);
        return new DevNameMappings(
            ExtractBlock(text, "hero_name_mapping"),
            ExtractBlock(text, "weapon_name_mapping"),
            ExtractBlock(text, "weapon_trait_name_mapping"));
    }

    private static readonly Regex PairPattern = new(@"'([^']+)'\s*:\s*[""']([^""']+)[""']", RegexOptions.Compiled);

    private static Dictionary<string, string> ExtractBlock(string text, string blockName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var startMarker = blockName + " = {";
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
            return result;

        var blockStart = text.IndexOf('{', start);
        if (blockStart < 0)
            return result;

        var blockEnd = text.IndexOf('}', blockStart);
        if (blockEnd < 0)
            return result;

        var block = text.Substring(blockStart, blockEnd - blockStart);
        foreach (Match m in PairPattern.Matches(block))
            result[m.Groups[1].Value] = m.Groups[2].Value;

        return result;
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run update-spells.cs <events-json> <target-cs>");
    return 1;
}

var jsonPath = Path.GetFullPath(args[0]);
var csPath = Path.GetFullPath(args[1]);

if (!File.Exists(jsonPath))
{
    Console.Error.WriteLine($"Input file not found: {jsonPath}");
    return 1;
}

if (!File.Exists(csPath))
{
    Console.Error.WriteLine($"Target file not found: {csPath}");
    return 1;
}

// Parse JSON and extract unique abilities by guid
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
var abilities = new Dictionary<int, (string Name, string? Icon)>();
ExtractAbilities(json.RootElement, abilities);

if (abilities.Count == 0)
{
    Console.WriteLine("No abilities found in JSON.");
    return 0;
}

Console.WriteLine($"Found {abilities.Count} unique abilities in JSON.");

// Read C# file and update spell definitions
var lines = await File.ReadAllLinesAsync(csPath);
var spellPattern = new Regex(@"new\((\d+),\s*""([^""]*?)""(?:,\s*""([^""]*)"")?\)");

var matchedIds = new HashSet<int>();
int updated = 0;

for (int i = 0; i < lines.Length; i++)
{
    var match = spellPattern.Match(lines[i]);
    if (!match.Success) continue;

    var id = int.Parse(match.Groups[1].Value);
    if (!abilities.TryGetValue(id, out var ability)) continue;

    matchedIds.Add(id);

    var existingName = match.Groups[2].Value;
    var existingIcon = match.Groups[3].Success ? match.Groups[3].Value : null;

    var finalName = ability.Name;
    var finalIcon = ability.Icon ?? existingIcon; // JSON is authoritative; keep existing if JSON has none

    if (existingName == finalName && existingIcon == finalIcon)
        continue;

    string replacement = finalIcon is not null
        ? $"new({id}, \"{finalName}\", \"{finalIcon}\")"
        : $"new({id}, \"{finalName}\")";

    lines[i] = lines[i][..match.Index] + replacement + lines[i][(match.Index + match.Length)..];

    var changes = new List<string>();
    if (existingName != finalName) changes.Add($"name: \"{existingName}\" -> \"{finalName}\"");
    if (existingIcon != finalIcon) changes.Add($"icon: \"{existingIcon}\" -> \"{finalIcon}\"");
    Console.WriteLine($"  Updated ID {id}: {string.Join(", ", changes)}");
    updated++;
}

// Report unmatched abilities (in JSON but not in C# file)
var unmatched = abilities.Keys.Except(matchedIds).Order().ToList();
if (unmatched.Count > 0)
{
    Console.WriteLine($"\n{unmatched.Count} abilities in JSON not found in {Path.GetFileName(csPath)}:");
    foreach (var id in unmatched)
        Console.WriteLine($"  ID {id}: \"{abilities[id].Name}\"");
}

if (updated > 0)
{
    await File.WriteAllLinesAsync(csPath, lines);
    Console.WriteLine($"\nUpdated {updated} spell(s) in {Path.GetFileName(csPath)}.");
}
else
{
    Console.WriteLine($"\nMatched {matchedIds.Count} spells, no changes needed.");
}

return 0;

// --- Helper methods ---

static void ExtractAbilities(JsonElement element, Dictionary<int, (string Name, string? Icon)> abilities)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            if (element.TryGetProperty("guid", out var guidProp) &&
                element.TryGetProperty("name", out var nameProp) &&
                guidProp.ValueKind == JsonValueKind.Number &&
                nameProp.ValueKind == JsonValueKind.String)
            {
                var id = guidProp.GetInt32();
                var name = nameProp.GetString()!;
                var icon = element.TryGetProperty("abilityIcon", out var iconProp) &&
                           iconProp.ValueKind == JsonValueKind.String
                    ? iconProp.GetString()
                    : null;
                abilities[id] = (name, icon);
            }
            else
            {
                foreach (var prop in element.EnumerateObject())
                    ExtractAbilities(prop.Value, abilities);
            }
            break;

        case JsonValueKind.Array:
            foreach (var item in element.EnumerateArray())
                ExtractAbilities(item, abilities);
            break;
    }
}

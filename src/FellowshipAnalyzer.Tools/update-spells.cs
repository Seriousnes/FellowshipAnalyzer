#:property PublishAot=false

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

// Parse JSON and extract unique abilities by guid.
// Effects have combat-log guids >= 1_000_000 (encoded as 1_000_000 + effectId);
// they are stored here by their base effectId to match the C# constructor argument.
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
var spells = new Dictionary<int, (string? Name, string? Icon)>();
var effects = new Dictionary<int, (string? Name, string? Icon)>();
ExtractAbilities(json.RootElement, spells, effects);

if (spells.Count == 0 && effects.Count == 0)
{
    Console.WriteLine("No abilities found in JSON.");
    return 0;
}

Console.WriteLine($"Found {spells.Count} spell(s) and {effects.Count} effect(s) in JSON.");

// Read C# file and update spell definitions
var lines = await File.ReadAllLinesAsync(csPath);
var spellPattern = new Regex(@"new\((\d+)(?:,\s*""([^""]*?)""(?:,\s*""([^""]*)"")?)?\)");

var matchedSpellIds = new HashSet<int>();
var matchedEffectIds = new HashSet<int>();
var allCsSpellIds = new HashSet<int>();
var allCsEffectIds = new HashSet<int>();
int updated = 0;

for (int i = 0; i < lines.Length; i++)
{
    var match = spellPattern.Match(lines[i]);
    if (!match.Success) continue;

    var id = int.Parse(match.Groups[1].Value);

    // Detect whether this line declares an Effect or a plain Spell by inspecting
    // the type name before the assignment — Effect guids are 1_000_000 + id.
    var beforeAssign = lines[i].Split('=')[0];
    bool isEffect = beforeAssign.Contains("Effect ");
    var lookup = isEffect ? effects : spells;
    var matchedIds = isEffect ? matchedEffectIds : matchedSpellIds;
    (isEffect ? allCsEffectIds : allCsSpellIds).Add(id);

    if (!lookup.TryGetValue(id, out var ability))
    {
        // abilities.json stores everything under spells; fall back for Effect declarations
        if (isEffect && spells.TryGetValue(id, out ability))
            matchedEffectIds.Add(id);
        else
            continue;
    }
    else
    {
        matchedIds.Add(id);
    }

    var existingName = match.Groups[2].Success ? match.Groups[2].Value : null;
    var existingIcon = match.Groups[3].Success ? match.Groups[3].Value : null;

    var finalName = ability.Name ?? existingName; // Keep existing name if JSON has none (e.g. abilities.json Name is null)
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

// Report .cs IDs that had no match in the JSON (likely wrong/stale IDs)
var unmatchedSpells = allCsSpellIds.Except(matchedSpellIds).Order().ToList();
var unmatchedEffects = allCsEffectIds.Except(matchedEffectIds).Order().ToList();
if (unmatchedSpells.Count > 0)
{
    Console.WriteLine($"\n{unmatchedSpells.Count} Spell ID(s) in {Path.GetFileName(csPath)} not found in JSON (verify these IDs are correct):");
    foreach (var id in unmatchedSpells)
        Console.WriteLine($"  Spell ID {id}");
}
if (unmatchedEffects.Count > 0)
{
    Console.WriteLine($"\n{unmatchedEffects.Count} Effect ID(s) in {Path.GetFileName(csPath)} not found in JSON (may not be in abilities.json \u2014 effects are not listed in the game API):");
    foreach (var id in unmatchedEffects)
        Console.WriteLine($"  Effect ID {id}");
}

if (updated > 0)
{
    await File.WriteAllLinesAsync(csPath, lines);
    Console.WriteLine($"\nUpdated {updated} spell(s)/effect(s) in {Path.GetFileName(csPath)}.");
}
else
{
    Console.WriteLine($"\nMatched {matchedSpellIds.Count} spell(s) and {matchedEffectIds.Count} effect(s), no changes needed.");
}

return 0;

// --- Helper methods ---

static void ExtractAbilities(
    JsonElement element,
    Dictionary<int, (string? Name, string? Icon)> spells,
    Dictionary<int, (string? Name, string? Icon)> effects)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            // API format (abilities.json): { "Id": 1318, "Name": "Multishot" | null, "Icon": "..." }
            // Name may be null; Icon is always populated. Stored in spells only — effect declarations
            // fall back to spells when their ID isn't in the effects dict (which is only populated
            // by the combat-log format path below).
            if (element.TryGetProperty("Id", out var idProp) &&
                element.TryGetProperty("Icon", out var apiIconProp) &&
                idProp.ValueKind == JsonValueKind.Number &&
                apiIconProp.ValueKind == JsonValueKind.String)
            {
                var id = idProp.GetInt32();
                var name = element.TryGetProperty("Name", out var apiNameProp) &&
                           apiNameProp.ValueKind == JsonValueKind.String
                    ? apiNameProp.GetString()
                    : null;
                var icon = apiIconProp.GetString();
                spells[id] = (name, icon);
            }
            // Combat-log export format: { "guid": 1318, "name": "Multishot", "abilityIcon": "..." }
            // Effects have guids >= 1_000_000; store by the base effectId so it
            // aligns with the constructor argument in the C# file (e.g. new(2312, ...)).
            else if (element.TryGetProperty("guid", out var guidProp) &&
                element.TryGetProperty("name", out var nameProp) &&
                guidProp.ValueKind == JsonValueKind.Number &&
                nameProp.ValueKind == JsonValueKind.String)
            {
                var guid = guidProp.GetInt32();
                var name = nameProp.GetString()!;
                var icon = element.TryGetProperty("abilityIcon", out var iconProp) &&
                           iconProp.ValueKind == JsonValueKind.String
                    ? iconProp.GetString()
                    : null;

                if (guid >= 1_000_000)
                    effects[guid - 1_000_000] = (name, icon);
                else
                    spells[guid] = (name, icon);
            }
            else
            {
                foreach (var prop in element.EnumerateObject())
                    ExtractAbilities(prop.Value, spells, effects);
            }
            break;

        case JsonValueKind.Array:
            foreach (var item in element.EnumerateArray())
                ExtractAbilities(item, spells, effects);
            break;
    }
}

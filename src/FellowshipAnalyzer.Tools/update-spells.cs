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

using var json = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
var spells = new Dictionary<int, (string? Name, string? Icon)>();
var effects = new Dictionary<int, (string? Name, string? Icon)>();
var talents = new Dictionary<int, (string? Name, string? Icon)>();
var weapons = new Dictionary<int, (string? Name, string? Icon)>();
ExtractAbilities(json.RootElement, spells, effects, talents, weapons);

if (spells.Count == 0 && effects.Count == 0 && talents.Count == 0 && weapons.Count == 0)
{
    Console.WriteLine("No abilities found in JSON.");
    return 0;
}

Console.WriteLine($"Found {spells.Count} spell(s), {effects.Count} effect(s), {talents.Count} talent(s), and {weapons.Count} weapon trait(s) in JSON.");

var lines = await File.ReadAllLinesAsync(csPath);
var spellPattern = new Regex(@"new\((\d+)(?:,\s*""([^""]*?)""(?:,\s*""([^""]*)"")?)?\)");

var matchedSpellIds = new HashSet<int>();
var matchedEffectIds = new HashSet<int>();
var matchedTalentIds = new HashSet<int>();
var matchedWeaponIds = new HashSet<int>();
var allCsSpellIds = new HashSet<int>();
var allCsEffectIds = new HashSet<int>();
var allCsTalentIds = new HashSet<int>();
var allCsWeaponIds = new HashSet<int>();
int updated = 0;

for (int i = 0; i < lines.Length; i++)
{
    var match = spellPattern.Match(lines[i]);
    if (!match.Success) continue;

    var id = int.Parse(match.Groups[1].Value);

    var beforeAssign = lines[i].Split('=')[0];
    bool isWeapon = beforeAssign.Contains("Weapon ");
    bool isTalent = !isWeapon && beforeAssign.Contains("Talent ");
    bool isEffect = !isWeapon && !isTalent && beforeAssign.Contains("Effect ");
    var lookup = isWeapon ? weapons : isTalent ? talents : isEffect ? effects : spells;
    var matchedIds = isWeapon ? matchedWeaponIds : isTalent ? matchedTalentIds : isEffect ? matchedEffectIds : matchedSpellIds;
    (isWeapon ? allCsWeaponIds : isTalent ? allCsTalentIds : isEffect ? allCsEffectIds : allCsSpellIds).Add(id);

    if (!lookup.TryGetValue(id, out var ability))
    {
        if ((isWeapon || isTalent || isEffect) && spells.TryGetValue(id, out ability))
            matchedIds.Add(id);
        else
            continue;
    }
    else
    {
        matchedIds.Add(id);
    }

    var existingName = match.Groups[2].Success ? match.Groups[2].Value : null;
    var existingIcon = match.Groups[3].Success ? match.Groups[3].Value : null;

    var finalName = ability.Name ?? existingName;
    var finalIcon = ability.Icon ?? existingIcon;

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

var unmatchedSpells = allCsSpellIds.Except(matchedSpellIds).Order().ToList();
var unmatchedEffects = allCsEffectIds.Except(matchedEffectIds).Order().ToList();
var unmatchedTalents = allCsTalentIds.Except(matchedTalentIds).Order().ToList();
var unmatchedWeapons = allCsWeaponIds.Except(matchedWeaponIds).Order().ToList();
if (unmatchedSpells.Count > 0)
{
    Console.WriteLine($"\n{unmatchedSpells.Count} Spell ID(s) in {Path.GetFileName(csPath)} not found in JSON (verify these IDs are correct):");
    foreach (var id in unmatchedSpells)
        Console.WriteLine($"  Spell ID {id}");
}
if (unmatchedEffects.Count > 0)
{
    Console.WriteLine($"\n{unmatchedEffects.Count} Effect ID(s) in {Path.GetFileName(csPath)} not found in JSON (may not be in abilities.json — effects are not listed in the game API):");
    foreach (var id in unmatchedEffects)
        Console.WriteLine($"  Effect ID {id}");
}
if (unmatchedTalents.Count > 0)
{
    Console.WriteLine($"\n{unmatchedTalents.Count} Talent ID(s) in {Path.GetFileName(csPath)} not found in JSON (may not be in abilities.json — talents are not listed in the game API):");
    foreach (var id in unmatchedTalents)
        Console.WriteLine($"  Talent ID {id}");
}
if (unmatchedWeapons.Count > 0)
{
    Console.WriteLine($"\n{unmatchedWeapons.Count} Weapon trait ID(s) in {Path.GetFileName(csPath)} not found in JSON (may not be in abilities.json — weapon traits are not listed in the game API):");
    foreach (var id in unmatchedWeapons)
        Console.WriteLine($"  Weapon trait ID {id}");
}

if (updated > 0)
{
    await File.WriteAllLinesAsync(csPath, lines);
    Console.WriteLine($"\nUpdated {updated} spell(s)/effect(s)/talent(s)/weapon trait(s) in {Path.GetFileName(csPath)}.");
}
else
{
    Console.WriteLine($"\nMatched {matchedSpellIds.Count} spell(s), {matchedEffectIds.Count} effect(s), {matchedTalentIds.Count} talent(s), and {matchedWeaponIds.Count} weapon trait(s), no changes needed.");
}

return 0;

static void ExtractAbilities(
    JsonElement element,
    Dictionary<int, (string? Name, string? Icon)> spells,
    Dictionary<int, (string? Name, string? Icon)> effects,
    Dictionary<int, (string? Name, string? Icon)> talents,
    Dictionary<int, (string? Name, string? Icon)> weapons)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
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

                if (guid >= 3_000_000)
                    weapons[guid - 3_000_000] = (name, icon);
                else if (guid >= 2_000_000)
                    talents[guid - 2_000_000] = (name, icon);
                else if (guid >= 1_000_000)
                    effects[guid - 1_000_000] = (name, icon);
                else
                    spells[guid] = (name, icon);
            }
            else
            {
                foreach (var prop in element.EnumerateObject())
                    ExtractAbilities(prop.Value, spells, effects, talents, weapons);
            }
            break;

        case JsonValueKind.Array:
            foreach (var item in element.EnumerateArray())
                ExtractAbilities(item, spells, effects, talents, weapons);
            break;
    }
}

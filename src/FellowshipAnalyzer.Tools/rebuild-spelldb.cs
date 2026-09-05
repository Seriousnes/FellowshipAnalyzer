#:property PublishAot=false
#:project ../FellowshipAnalyzer.SpellData/FellowshipAnalyzer.SpellData.csproj

using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;

var result = MergeEngine.Run(MergeInputs.Load());
var json = SpellDbWriter.Serialize(result);
Directory.CreateDirectory(Path.GetDirectoryName(SourcePaths.SpellDb)!);
File.WriteAllText(SourcePaths.SpellDb, json);

var writtenSpells = result.Spells.Count(s => MemberNaming.IsValidIdentifier(s.Member));
var writtenTalents = result.Talents.Count(t => MemberNaming.IsValidIdentifier(t.Member));
var skippedCount = result.Spells.Count + result.Talents.Count - writtenSpells - writtenTalents;
Console.WriteLine($"Wrote {writtenSpells} spells and {writtenTalents} talents to {SourcePaths.SpellDb} ({result.Gaps.Count} gap(s); {skippedCount} skipped).");

foreach (var (section, entries) in new (string Section, List<CuratedSpell> Entries)[]
         {
             ("spells", result.Spells),
             ("talents", result.Talents),
         })
{
    var collisions = entries
        .Where(s => MemberNaming.IsValidIdentifier(s.Member))
        .GroupBy(s => (s.Scope, s.Member))
        .Where(g => g.Count() > 1)
        .ToList();

    if (collisions.Count == 0)
        continue;

    Console.WriteLine($"  COLLISION in {section} ({collisions.Count} duplicate scope.member pairs - two entries share a sanitized name):");
    foreach (var g in collisions.OrderBy(g => g.Key.Scope).ThenBy(g => g.Key.Member))
        Console.WriteLine($"    [{g.Key.Scope}.{g.Key.Member}] ids: {string.Join(", ", g.Select(s => s.Spell.Id))}");
}

foreach (var gap in result.Gaps.OrderBy(g => g.Scope).ThenBy(g => g.Member).ThenBy(g => g.Kind))
    Console.WriteLine($"  GAP [{gap.Kind}] {gap.Scope}.{gap.Member}");
return 0;

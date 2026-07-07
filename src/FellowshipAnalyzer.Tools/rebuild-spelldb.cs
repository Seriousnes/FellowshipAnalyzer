#:property PublishAot=false
#:project ../FellowshipAnalyzer.SpellData/FellowshipAnalyzer.SpellData.csproj

using FellowshipAnalyzer.SpellData;

var result = MergeEngine.Run(MergeInputs.Load());
var json = SpellDbWriter.Serialize(result);
Directory.CreateDirectory(Path.GetDirectoryName(SourcePaths.SpellDb)!);
File.WriteAllText(SourcePaths.SpellDb, json);

var writtenCount = result.Spells.Count(s => MemberNaming.IsValidIdentifier(s.Member));
var skippedCount = result.Spells.Count - writtenCount;
Console.WriteLine($"Wrote {writtenCount} spells to {SourcePaths.SpellDb} ({result.Gaps.Count} gap(s); {skippedCount} skipped).");

var collisions = result.Spells
    .Where(s => MemberNaming.IsValidIdentifier(s.Member))
    .GroupBy(s => (s.Scope, s.Member))
    .Where(g => g.Count() > 1)
    .ToList();

if (collisions.Count > 0)
{
    Console.WriteLine($"  COLLISION ({collisions.Count} duplicate scope.member pairs — two abilities share a sanitized name; needs Task 13 overrides):");
    foreach (var g in collisions.OrderBy(g => g.Key.Scope).ThenBy(g => g.Key.Member))
        Console.WriteLine($"    [{g.Key.Scope}.{g.Key.Member}] ids: {string.Join(", ", g.Select(s => s.Spell.Id))}");
}

foreach (var gap in result.Gaps.OrderBy(g => g.Scope).ThenBy(g => g.Member).ThenBy(g => g.Kind))
    Console.WriteLine($"  GAP [{gap.Kind}] {gap.Scope}.{gap.Member}");
return 0;

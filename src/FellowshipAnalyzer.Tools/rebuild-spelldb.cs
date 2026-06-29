#:property PublishAot=false
#:project ../FellowshipAnalyzer.SpellData/FellowshipAnalyzer.SpellData.csproj

using FellowshipAnalyzer.SpellData;

var result = MergeEngine.Run(MergeInputs.Load());
var json = SpellDbWriter.Serialize(result);
Directory.CreateDirectory(Path.GetDirectoryName(SourcePaths.SpellDb)!);
File.WriteAllText(SourcePaths.SpellDb, json);

var writtenCount = result.Spells.Count(s => MemberNaming.IsValidIdentifier(s.Member));
Console.WriteLine($"Wrote {writtenCount} spells to {SourcePaths.SpellDb} ({result.Gaps.Count} gap(s); {result.Spells.Count - writtenCount} skipped).");
foreach (var gap in result.Gaps.OrderBy(g => g.Scope).ThenBy(g => g.Member).ThenBy(g => g.Kind))
    Console.WriteLine($"  GAP [{gap.Kind}] {gap.Scope}.{gap.Member}");
return 0;

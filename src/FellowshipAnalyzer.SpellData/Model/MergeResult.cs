namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>Placeholder provenance record; full per-field sourcing is added in Task 9.</summary>
public record Provenance;

/// <summary>A detected gap in the merged spell data (missing name, icon, or unresolved entry).</summary>
public record Gap(string Scope, string Member, string Reason);

/// <summary>The output of <see cref="MergeEngine.Run"/>: selected spells and detected gaps.</summary>
public record MergeResult(IReadOnlyList<MergedSpell> Spells, IReadOnlyList<Gap> Gaps);

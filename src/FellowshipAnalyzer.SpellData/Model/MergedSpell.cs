namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>A spell record merged from all upstream game-data sources.</summary>
public record MergedSpell(int FslId, SpellKind Kind, string? Name, string DevName);

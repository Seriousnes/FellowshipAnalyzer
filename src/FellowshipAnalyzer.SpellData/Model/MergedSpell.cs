namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>A spell record merged and enriched from all upstream game-data sources.</summary>
public record MergedSpell(
    string Scope,
    string Member,
    int Id,
    SpellKind Kind,
    string Name,
    string Icon,
    double? Cooldown,
    int? Range,
    int Charges,
    double? CastDuration,
    double? ChannelDuration,
    double? ChannelTickInterval,
    IReadOnlyDictionary<string, int> Costs,
    Provenance Provenance)
{
    /// <summary>The full FSL guid for this spell, encoding kind and native id.</summary>
    public int Guid => SpellKindRange.GuidFor(Kind, Id);
}

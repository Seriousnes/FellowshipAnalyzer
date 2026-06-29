namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>Identifies which upstream data source supplied a particular field value in a <see cref="MergedSpell"/>.</summary>
public enum ProvenanceSource { SpellData, HeroData, GearData, Icons, Override }

/// <summary>Per-field record of which upstream source supplied each value in a <see cref="MergedSpell"/>.</summary>
public record Provenance(
    ProvenanceSource? Id = null,
    ProvenanceSource? Kind = null,
    ProvenanceSource? Name = null,
    ProvenanceSource? Icon = null,
    ProvenanceSource? Cooldown = null,
    ProvenanceSource? Range = null,
    ProvenanceSource? Charges = null,
    ProvenanceSource? CastDuration = null,
    ProvenanceSource? ChannelDuration = null,
    ProvenanceSource? ChannelTickInterval = null,
    ProvenanceSource? Costs = null);

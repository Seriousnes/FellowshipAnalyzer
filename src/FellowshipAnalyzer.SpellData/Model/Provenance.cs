namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>Identifies which upstream data source supplied a particular field value in a <see cref="CuratedSpell"/>.</summary>
public enum ProvenanceSource { SpellData, HeroData, GearData, Icons, Override }

/// <summary>
/// Per-field record of which upstream source supplied each value, keyed by the camelCase json
/// field name (the same naming used for serialization). A field with no entry has no provenance.
/// </summary>
public sealed record Provenance(IReadOnlyDictionary<string, ProvenanceSource> ByField)
{
    /// <summary>A provenance with no recorded fields.</summary>
    public static Provenance Empty { get; } = new(new Dictionary<string, ProvenanceSource>());

    /// <summary>The source recorded for <paramref name="field"/>, or <c>null</c> if none.</summary>
    public ProvenanceSource? For(string field) => ByField.TryGetValue(field, out var source) ? source : null;
}

/// <summary>Accumulates per-field provenance entries during a merge.</summary>
public sealed class ProvenanceBuilder
{
    private readonly Dictionary<string, ProvenanceSource> _byField = new(StringComparer.Ordinal);

    public ProvenanceBuilder Set(string field, ProvenanceSource source)
    {
        _byField[field] = source;
        return this;
    }

    public ProvenanceBuilder SetIf(string field, bool condition, ProvenanceSource source)
    {
        if (condition)
            _byField[field] = source;
        return this;
    }

    public Provenance Build() => new(_byField);
}

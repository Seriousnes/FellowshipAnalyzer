namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Describes a link relationship between events. Used to track which event links have been processed.
/// </summary>
public readonly struct EventLink : IEquatable<EventLink>
{
    /// <summary>The name of the relationship this link represents, identifying which normalizer step established the pairing.</summary>
    public string LinkRelation { get; init; }

    /// <summary>Compares this link to <paramref name="other"/> by <see cref="LinkRelation"/>.</summary>
    public bool Equals(EventLink other) => LinkRelation == other.LinkRelation;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EventLink other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => LinkRelation?.GetHashCode() ?? 0;
}

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Describes a link relationship between events. Used to track which event links have been processed.
/// </summary>
public readonly struct EventLink : IEquatable<EventLink>
{
    public string LinkRelation { get; init; }

    public bool Equals(EventLink other) => LinkRelation == other.LinkRelation;
    public override bool Equals(object? obj) => obj is EventLink other && Equals(other);
    public override int GetHashCode() => LinkRelation?.GetHashCode() ?? 0;
}

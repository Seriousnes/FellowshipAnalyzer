using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// One rule an <see cref="Analysis.Normalizers.EventLinkNormalizer"/> applies: the events that link,
/// the events they link to, and how far apart in time the two may be.
/// <para>
/// A match adds the referenced event to the linking event's <see cref="Event.LinkedEvents"/> under
/// <see cref="Relation"/>, so the linking event is the one that reads the association back.
/// Both event types match by assignability, so naming a base type such as <see cref="BuffEvent"/>
/// matches every event deriving from it.
/// </para>
/// </summary>
public sealed record EventLink
{
    /// <summary>The relationship name the resulting <see cref="LinkedEvent"/> is read back by.</summary>
    public required string Relation { get; init; }

    /// <summary>The event type that adds links.</summary>
    public required Type LinkingEventType { get; init; }

    /// <summary>The abilities a linking event may name. <c>null</c> matches an event of any ability, and an event naming no ability at all.</summary>
    public List<FSLID>? LinkingAbilityIds { get; init; }

    /// <summary>The event type that is linked to.</summary>
    public required Type ReferencedEventType { get; init; }

    /// <summary>The abilities a referenced event may name. <c>null</c> matches an event of any ability, and an event naming no ability at all.</summary>
    public List<FSLID>? ReferencedAbilityIds { get; init; }

    /// <summary>How far after the linking event a referenced event may be, in milliseconds. Zero links only within the same timestamp.</summary>
    public int ForwardBufferMs { get; init; }

    /// <summary>How far before the linking event a referenced event may be, in milliseconds. Zero links only within the same timestamp.</summary>
    public int BackwardBufferMs { get; init; }

    /// <summary>Links two events that name different sources. Off by default, so both events must come from the same actor.</summary>
    public bool AnySource { get; init; }

    /// <summary>Links two events that name different targets. Off by default, so both events must name the same target and instance.</summary>
    public bool AnyTarget { get; init; }
}

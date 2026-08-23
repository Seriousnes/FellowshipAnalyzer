using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Base type shared by <see cref="CastEvent"/>, <see cref="FreeCastEvent"/>, <see cref="LeechEvent"/> and <see cref="FilterCooldownInfoEvent"/>.</summary>
public abstract class BaseCastEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent
{
    /// <summary>The ability being cast.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID of the cast ability, as reported by FellowshipLogs.</summary>
    public FSLID AbilityGameId { get; set; }
    /// <summary>The shield absorb amount active on the source when the cast landed.</summary>
    public virtual int? Absorb { get; set; }
    /// <summary>The channel this cast started, linked in by <see cref="Analysis.Normalizers.CastLinkNormalizer"/>.</summary>
    public virtual EndChannelEvent? Channel { get; set; }
    /// <summary>The unique id of the actor performing the cast.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Disambiguates the source when multiple copies of the same enemy share <see cref="SourceId"/>.</summary>
    public virtual int? SourceInstance { get; set; }
    /// <summary>Whether the casting actor is friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>The FellowshipLogs actor descriptor for the cast's target.</summary>
    public virtual ICastTarget Target { get; set; }
    /// <summary>The unique id of the actor the cast targeted.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Disambiguates the target when multiple copies of the same enemy share <see cref="TargetId"/>.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Whether the targeted actor is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>The global cooldown this cast triggered, linked in by <see cref="Analysis.GlobalCooldown"/>.</summary>
    public virtual GlobalCooldownEvent? GlobalCooldown { get; set; }
    /// <summary>Untyped metadata FellowshipLogs attaches to the cast; content varies by ability.</summary>
    public virtual object? Meta { get; set; }
}

/// <summary>The FellowshipLogs <c>cast</c> event, raised when an ability cast completes successfully.</summary>
public class CastEvent : BaseCastEvent
{
    /// <summary>FellowshipLogs synthetic event - not a real player action.</summary>
    public virtual bool Fake { get; set; }

    /// <summary>FellowshipLogs cast-start marker (beginning of a cast with cast time).</summary>
    public virtual bool Activation { get; set; }
}
/// <summary>The FellowshipLogs <c>freecast</c> event, raised for a cast detected as having no resource cost.</summary>
public class FreeCastEvent : BaseCastEvent { }
/// <summary>The FellowshipLogs <c>leech</c> event, raised for a cast triggered by a life-leech effect.</summary>
public class LeechEvent : BaseCastEvent { }
/// <summary>
/// The FellowshipLogs <c>cooldowninfo</c> event, carrying cooldown metadata that arrives ahead of the
/// full <see cref="CastEvent"/>; <see cref="Analysis.SpellUsable"/> uses it to begin an ability's cooldown early.
/// </summary>
public class FilterCooldownInfoEvent : BaseCastEvent
{
}

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Common;

/// <summary>How a damage, heal, or ability event resolved against its target.</summary>
public enum HitType
{
    /// <summary>The attack missed, dealing no damage.</summary>
    Miss = 0,
    /// <summary>A regular, non-critical hit.</summary>
    Normal = 1,
    /// <summary>A critical hit.</summary>
    Crit = 2,
    /// <summary>
    /// A hit the target blocked. Carries a non-zero <see cref="DamageEvent.Blocked"/>, and often
    /// reduces the hit to nothing; only heroes with a block chance produce it.
    /// </summary>
    Block = 4,
    /// <summary>The attack was dodged, dealing no damage.</summary>
    Dodge = 7,
    /// <summary>The attack was parried, dealing no damage.</summary>
    Parry = 8,
    /// <summary>Guarantee crit dealing additional damage based on crit chance.</summary>
    GrievousCrit = 22
}

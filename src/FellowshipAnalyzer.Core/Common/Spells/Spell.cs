using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// A static spell definition: identity, physical facts (cooldown, range, charges,
/// cast/channel timing), and resource costs. Behaviour metadata (GCD, category, haste
/// scaling) lives on <see cref="Analysis.SpellbookAbility"/>.
/// </summary>
/// <remarks>
/// During the registry migration the positional constructor is retained with every
/// parameter optional so the hand-written <c>new(id, name, icon)</c> form and the
/// generated <c>new { Id = …, Cooldown = … }</c> form compile simultaneously. The final
/// cleanup task drops the positional constructor.
/// </remarks>
public record Spell(int Id = 0, string Name = "", string Icon = "") : IRimeSpell, IElarionSpell
{
    /// <summary>The combat-log <c>abilityGameID</c> used to match events. Base spells use <see cref="Id"/>; subtypes add their FSL range offset.</summary>
    public virtual int Guid => Id;

    public double? Cooldown { get; init; }
    public int? Range { get; init; }
    public int Charges { get; init; } = 1;
    public double? CastDuration { get; init; }
    public double? ChannelDuration { get; init; }
    public double? ChannelTickInterval { get; init; }

    public virtual int? SpiritCost { get; init; }
    public virtual int? WinterOrbCost { get; init; }
    public virtual int? AnimaCost { get; init; }
    public int? FocusCost { get; init; }

    /// <summary>
    /// Creates the typed spell for a combat-log <c>abilityGameID</c>, decoding the FSL range:
    /// ability → <see cref="Spell"/>, effect → <see cref="Effect"/>, talent → <see cref="Talent"/>, weapon trait → <see cref="Weapon"/>.
    /// </summary>
    /// <remarks>
    /// FellowshipLogs partitions every entity into its own "million" range so API IDs from
    /// different entity types never collide; the native game ID is recovered by stripping the
    /// range offset:
    /// <list type="table">
    ///   <listheader><term>FSL ID range</term><description>Entity type → native game ID</description></listheader>
    ///   <item><term>0 – 999,999</term><description>Ability (<c>GA_</c>: cast / channel / auto-attack / passive) → ID (no offset)</description></item>
    ///   <item><term>1,000,000 – 1,999,999</term><description>Effect (<c>GE_</c>: buff / debuff / damage / heal) → ID − 1,000,000</description></item>
    ///   <item><term>2,000,000 – 2,999,999</term><description>Talent (<c>CAATalent*</c>) → ID − 2,000,000</description></item>
    ///   <item><term>3,000,000 +</term><description>Weapon Trait → ID − 3,000,000</description></item>
    /// </list>
    /// The buffer disambiguates native game IDs that collide across types — e.g. game ID 1312 is
    /// <c>GA_Bowguy_ChanneledMultiProjectileDebuff</c> (Heartseeker Barrage) as an ability and a
    /// <c>GE_Vigor</c> stun as an effect. This switch decodes all four ranges.
    /// </remarks>
    public static Spell FromGuid(int guid, string name = "", string icon = "") => guid switch
    {
        >= 3_000_000 => new Weapon(guid - 3_000_000, name, icon),
        >= 2_000_000 => new Talent(guid - 2_000_000, name, icon),
        >= 1_000_000 => new Effect(guid - 1_000_000, name, icon),
        _ => new Spell(guid, name, icon),
    };
}

/// <summary>A spell effect (<c>GE_</c>): combat-log guid <c>1_000_000 + Id</c>.</summary>
public record Effect(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
{
    public override int Guid => 1_000_000 + Id;
}

/// <summary>A talent (<c>CAATalent*</c>): combat-log guid <c>2_000_000 + Id</c>.</summary>
public record Talent(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
{
    public override int Guid => 2_000_000 + Id;
}

/// <summary>A weapon trait: combat-log guid <c>3_000_000 + Id</c>.</summary>
public record Weapon(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
{
    public override int Guid => 3_000_000 + Id;
}

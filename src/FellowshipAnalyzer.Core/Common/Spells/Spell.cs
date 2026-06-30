using System.Text.Json.Serialization;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// A static spell definition: identity, physical facts (cooldown, range, charges,
/// cast/channel timing), and resource costs. Behaviour metadata (GCD, category, haste
/// scaling) lives on <see cref="Analysis.SpellbookAbility"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Spell), "ability")]
[JsonDerivedType(typeof(Effect), "effect")]
[JsonDerivedType(typeof(Talent), "talent")]
[JsonDerivedType(typeof(Weapon), "weapon")]
public record Spell : IRimeSpell, IElarionSpell
{
    /// <summary>The combat-log <c>abilityGameID</c> used to match events. Base spells use <see cref="Id"/>; subtypes add their FSL range offset.</summary>
    [JsonIgnore]
    public virtual int Guid => Id;

    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    public double? Cooldown { get; init; }
    public int? Range { get; init; }
    public int Charges { get; init; } = 1;
    public double? CastDuration { get; init; }
    public double? ChannelDuration { get; init; }
    public double? ChannelTickInterval { get; init; }

    [JsonIgnore] public virtual int? SpiritCost { get; init; }
    [JsonIgnore] public virtual int? WinterOrbCost { get; init; }
    [JsonIgnore] public virtual int? AnimaCost { get; init; }
    [JsonIgnore] public int? FocusCost { get; init; }

    /// <summary>Resource costs keyed by abstract <see cref="ResourceTypes"/> slot; empty when the spell spends nothing.</summary>
    public IReadOnlyDictionary<ResourceTypes, int> Costs { get; init; } =
        System.Collections.Frozen.FrozenDictionary<ResourceTypes, int>.Empty;

    /// <summary>The cost in the given resource slot, or <c>null</c> when the spell does not spend it.</summary>
    public int? Cost(ResourceTypes type) => Costs.TryGetValue(type, out var value) ? value : null;

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
        >= 3_000_000 => new Weapon { Id = guid - 3_000_000, Name = name, Icon = icon },
        >= 2_000_000 => new Talent { Id = guid - 2_000_000, Name = name, Icon = icon },
        >= 1_000_000 => new Effect { Id = guid - 1_000_000, Name = name, Icon = icon },
        _ => new Spell { Id = guid, Name = name, Icon = icon },
    };
}

/// <summary>A spell effect (<c>GE_</c>): combat-log guid <c>1_000_000 + Id</c>.</summary>
public record Effect : Spell
{
    [JsonIgnore]
    public override int Guid => 1_000_000 + Id;
}

/// <summary>A talent (<c>CAATalent*</c>): combat-log guid <c>2_000_000 + Id</c>.</summary>
public record Talent : Spell
{
    [JsonIgnore]
    public override int Guid => 2_000_000 + Id;
}

/// <summary>A weapon trait: combat-log guid <c>3_000_000 + Id</c>.</summary>
public record Weapon : Spell
{
    [JsonIgnore]
    public override int Guid => 3_000_000 + Id;
}

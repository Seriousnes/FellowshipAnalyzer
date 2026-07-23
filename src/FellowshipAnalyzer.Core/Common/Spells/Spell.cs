using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
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
public record Spell : IRimeSpell, IElarionSpell, IArdeosSpell
{
    /// <summary>The FellowshipLogs id (combat-log <c>abilityGameID</c>). Base spells use <see cref="Id"/>; subtypes add their range offset.</summary>
    [JsonIgnore]
    public virtual FSLID FSLID => new FSLID(Id);

    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    public double? Cooldown { get; init; }

    /// <summary>Seconds removed from this ability's remaining cooldown when a target it has debuffed dies, per qualifying window.</summary>
    public double? CooldownReductionOnTargetDeath { get; init; }

    public int? Range { get; init; }
    public int Charges { get; init; } = 1;
    public double? CastDuration { get; init; }
    public double? ChannelDuration { get; init; }
    public double? ChannelTickInterval { get; init; }

    [JsonIgnore] 
    public int? SpiritCost => Cost(ResourceTypes.Spirit);
    [JsonIgnore] 
    public int? WinterOrbCost => Cost(ResourceTypes.Tertiary);
    [JsonIgnore] 
    public int? AnimaCost => Cost(ResourceTypes.Primary);
    [JsonIgnore] 
    public int? FocusCost => Cost(ResourceTypes.Primary);
    [JsonIgnore] 
    public int? EmberCost => Cost(ResourceTypes.Primary);

    /// <summary>Resource costs keyed by abstract <see cref="ResourceTypes"/> slot; empty when the spell spends nothing.</summary>
    public IReadOnlyDictionary<ResourceTypes, int> Costs { get; init; } =
        System.Collections.Frozen.FrozenDictionary<ResourceTypes, int>.Empty;

    /// <summary>The cost in the given resource slot, or <c>null</c> when the spell does not spend it.</summary>
    public int? Cost(ResourceTypes type) => Costs.TryGetValue(type, out var value) ? value : null;

    /// <summary>Creates the typed spell for a combat-log <c>abilityGameID</c>, decoding the FSL range via <see cref="FSLID"/>.</summary>
    public static Spell FromFSLID(FSLID fslid, string name = "", string icon = "") => fslid.Kind switch
    {
        SpellKind.Weapon => new Weapon { Id = fslid.NativeId, Name = name, Icon = icon },
        SpellKind.Talent => new Talent { Id = fslid.NativeId, Name = name, Icon = icon },
        SpellKind.Effect => new Effect { Id = fslid.NativeId, Name = name, Icon = icon },
        _ => new Spell { Id = fslid.NativeId, Name = name, Icon = icon },
    };
}

/// <summary>A spell effect (<c>GE_</c>): namespaced id <c>1_000_000 + Id</c>.</summary>
public record Effect : Spell
{
    [JsonIgnore]
    public override FSLID FSLID => FSLID.FromNative(SpellKind.Effect, Id);
}

/// <summary>A talent (<c>CAATalent*</c>): namespaced id <c>2_000_000 + Id</c>.</summary>
public record Talent : Spell
{
    [JsonIgnore]
    public override FSLID FSLID => FSLID.FromNative(SpellKind.Talent, Id);
}

/// <summary>A weapon trait: namespaced id <c>3_000_000 + Id</c>.</summary>
public record Weapon : Spell
{
    [JsonIgnore]
    public override FSLID FSLID => FSLID.FromNative(SpellKind.Weapon, Id);
}

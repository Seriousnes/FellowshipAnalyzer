using System.Diagnostics;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public interface ISpell
{
    int Id { get; }
    FSLID FSLID { get; }
}

[DebuggerDisplay("{Name,nq} - FSLID: {FSLID}")]
public class Ability : ISpell
{
    [JsonIgnore]
    public virtual int Id { get => FSLID; set => FSLID = value; }

    [JsonPropertyName("guid")]
    public virtual FSLID FSLID { get; set; }

    public virtual string Name { get; set; } = string.Empty;

    [JsonPropertyName("abilityIcon")]
    public virtual string Icon { get; set; } = string.Empty;

    public virtual MagicSchool Type { get; set; }

    public static readonly Ability UnknownAbility = new() { Id = 0, Name = "Unknown" };
}
